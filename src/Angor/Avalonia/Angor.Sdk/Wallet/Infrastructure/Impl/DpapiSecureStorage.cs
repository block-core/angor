using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

/// Windows DPAPI-based secure storage for master encryption keys.
public class DpapiSecureStorage : ISecureStorage
{
    private const int KeySizeInBytes = 32; // 256-bit key for AES-GCM
    private const string SecureDirectoryPath = "Angor";
    private const string SecureSubDirectory = "secure";
    private const string KeyFileExtension = ".key";
    
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    /// Stores a master encryption key securely using Windows DPAPI.
    /// If no key is provided, generates a new one.
    public async Task<Result<string>> StoreMasterKeyAsync(
        string walletId,
        string? masterKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(walletId))
                return Result.Failure<string>("Wallet ID cannot be empty");

            // Generate new key if not provided
            string keyToStore = masterKey ?? GenerateMasterKey();

            // Validate key format (should be base64 encoded 32-byte key)
            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(keyToStore);
                if (keyBytes.Length != KeySizeInBytes)
                    return Result.Failure<string>($"Master key must be {KeySizeInBytes} bytes ({KeySizeInBytes * 8}-bit)");
            }
            catch (FormatException)
            {
                return Result.Failure<string>("Master key must be valid base64 encoded string");
            }

            // Encrypt using DPAPI
            byte[] protectedKey = ProtectedData.Protect(
                keyBytes, 
                GetEntropy(walletId), 
                DataProtectionScope.CurrentUser);

            // Store in file system with thread safety
            string keyFilePath = GetKeyFilePath(walletId);
            
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                EnsureSecureDirectoryExists();
                
                await File.WriteAllBytesAsync(
                    keyFilePath, 
                    protectedKey, 
                    cancellationToken);
                
                // Set file attributes to hidden and system for additional security
                File.SetAttributes(keyFilePath, FileAttributes.ReadOnly | FileAttributes.System);
                
                Console.WriteLine($"[SecureStorage] Master key stored for wallet: {walletId}");
                Console.WriteLine($"[SecureStorage] Key file: {keyFilePath}");
            }
            finally
            {
                _fileLock.Release();
            }

            // Clear sensitive data from memory
            Array.Clear(keyBytes, 0, keyBytes.Length);

            return Result.Success(keyToStore);
        }
        catch (CryptographicException ex)
        {
            return Result.Failure<string>($"DPAPI encryption failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure<string>($"Access denied to secure storage: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result.Failure<string>($"File system error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Error storing master key: {ex.Message}");
        }
    }

    /// Retrieves a master encryption key from secure storage.
    public async Task<Result<string>> GetMasterKeyAsync(
        string walletId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(walletId))
                return Result.Failure<string>("Wallet ID cannot be empty");

            string keyFilePath = GetKeyFilePath(walletId);

            if (!File.Exists(keyFilePath))
                return Result.Failure<string>($"Master key not found for wallet '{walletId}'");

            // Read and decrypt with thread safety
            byte[] protectedKey;
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                protectedKey = await File.ReadAllBytesAsync(keyFilePath, cancellationToken);
            }
            finally
            {
                _fileLock.Release();
            }

            // Decrypt using DPAPI
            byte[] masterKeyBytes = ProtectedData.Unprotect(
                protectedKey, 
                GetEntropy(walletId), 
                DataProtectionScope.CurrentUser);

            string masterKey = Convert.ToBase64String(masterKeyBytes);
            Console.WriteLine($"[SecureStorage] Master key retrieved for wallet: {walletId}");
            
            // Clear sensitive data from memory
            Array.Clear(masterKeyBytes, 0, masterKeyBytes.Length);
            Array.Clear(protectedKey, 0, protectedKey.Length);

            return Result.Success(masterKey);
        }
        catch (CryptographicException ex)
        {
            return Result.Failure<string>(
                $"Failed to decrypt master key. The key may have been encrypted by a different user or on a different machine. DPAPI error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure<string>($"Access denied to secure storage: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result.Failure<string>($"File system error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Error retrieving master key: {ex.Message}");
        }
    }

    public async Task<Result<bool>> HasMasterKeyAsync(
        string walletId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(walletId))
                return Result.Failure<bool>("Wallet ID cannot be empty");

            string keyFilePath = GetKeyFilePath(walletId);
            
            bool exists = await Task.Run(() => File.Exists(keyFilePath), cancellationToken);
            
            return Result.Success(exists);
        }
        catch (Exception ex)
        {
            return Result.Failure<bool>($"Error checking master key existence: {ex.Message}");
        }
    }

    public async Task<Result> DeleteMasterKeyAsync(
        string walletId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(walletId))
                return Result.Failure("Wallet ID cannot be empty");

            string keyFilePath = GetKeyFilePath(walletId);

            if (!File.Exists(keyFilePath))
                return Result.Failure($"Master key not found for wallet '{walletId}'");

            // Delete with thread safety
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                // Remove read-only/hidden attributes before deletion
                if (File.Exists(keyFilePath))
                {
                    File.SetAttributes(keyFilePath, FileAttributes.Normal);
                    File.Delete(keyFilePath);
                }
            }
            finally
            {
                _fileLock.Release();
            }

            return Result.Success();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure($"Access denied when deleting master key: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result.Failure($"File system error when deleting master key: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error deleting master key: {ex.Message}");
        }
    }

    /// Updates the master encryption key for a wallet.
    /// Validates the new key before replacing the old one.
    public async Task<Result<string>> UpdateMasterKeyAsync(
        string walletId,
        string newMasterKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(walletId))
                return Result.Failure<string>("Wallet ID cannot be empty");

            if (string.IsNullOrWhiteSpace(newMasterKey))
                return Result.Failure<string>("New master key cannot be empty");

            // Validate new key format
            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(newMasterKey);
                if (keyBytes.Length != KeySizeInBytes)
                    return Result.Failure<string>($"Master key must be {KeySizeInBytes} bytes ({KeySizeInBytes * 8}-bit)");
            }
            catch (FormatException)
            {
                return Result.Failure<string>("Master key must be valid base64 encoded string");
            }

            string keyFilePath = GetKeyFilePath(walletId);

            // Check if old key exists
            if (!File.Exists(keyFilePath))
                return Result.Failure<string>($"Master key not found for wallet '{walletId}'. Use StoreMasterKeyAsync instead.");

            // Encrypt new key using DPAPI
            byte[] protectedKey = ProtectedData.Protect(
                keyBytes, 
                GetEntropy(walletId), 
                DataProtectionScope.CurrentUser);

            // Update file with thread safety
            await _fileLock.WaitAsync(cancellationToken);
            try
            {
                // Remove attributes to allow writing
                File.SetAttributes(keyFilePath, FileAttributes.Normal);
                
                await File.WriteAllBytesAsync(
                    keyFilePath, 
                    protectedKey, 
                    cancellationToken);
                
                // Restore security attributes
                File.SetAttributes(keyFilePath, FileAttributes.Hidden | FileAttributes.System);
            }
            finally
            {
                _fileLock.Release();
            }

            // Clear sensitive data from memory
            Array.Clear(keyBytes, 0, keyBytes.Length);

            return Result.Success(newMasterKey);
        }
        catch (CryptographicException ex)
        {
            return Result.Failure<string>($"DPAPI encryption failed: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure<string>($"Access denied to secure storage: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Result.Failure<string>($"File system error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Error updating master key: {ex.Message}");
        }
    }

    private static string GenerateMasterKey()
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] keyBytes = new byte[KeySizeInBytes];
        rng.GetBytes(keyBytes);
        string key = Convert.ToBase64String(keyBytes);
        Array.Clear(keyBytes, 0, keyBytes.Length);
        return key;
    }

    private static string GetKeyFilePath(string walletId)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, SecureDirectoryPath, SecureSubDirectory, $"{walletId}{KeyFileExtension}");
    }

    private static void EnsureSecureDirectoryExists()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string securePath = Path.Combine(appDataPath, SecureDirectoryPath, SecureSubDirectory);
        
        if (!Directory.Exists(securePath))
        {
            var dirInfo = Directory.CreateDirectory(securePath);
            // Hide the directory for additional security
            dirInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
        }
    }

    private static byte[] GetEntropy(string walletId)
    {
        string entropyString = $"Angor.Wallet.{walletId}";
        return System.Text.Encoding.UTF8.GetBytes(entropyString);
    }
}
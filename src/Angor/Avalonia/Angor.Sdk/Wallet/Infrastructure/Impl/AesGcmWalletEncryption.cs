using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class AesGcmWalletEncryption : IWalletEncryption
{
    private const int KeySizeInBytes = 32;
    private const int NonceSizeInBytes = 12;
    private const int TagSizeInBytes = 16;

    private readonly ISecureStorage _secureStorage;

    public AesGcmWalletEncryption(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
    }

    public async Task<Result<EncryptedWallet>> EncryptAsync(
        WalletData walletData,
        string masterKey,
        string name,
        string id)
    {
        if (walletData == null)
            return Result.Failure<EncryptedWallet>("Wallet data cannot be null");
        if (string.IsNullOrWhiteSpace(masterKey))
            return Result.Failure<EncryptedWallet>("Master key cannot be empty");
        if (string.IsNullOrWhiteSpace(id))
            return Result.Failure<EncryptedWallet>("Wallet ID cannot be empty");

        try
        {
            var keyBytes = Convert.FromBase64String(masterKey);
            if (keyBytes.Length != KeySizeInBytes)
            {
                Array.Clear(keyBytes, 0, keyBytes.Length);
                return Result.Failure<EncryptedWallet>($"Invalid master key size. Expected {KeySizeInBytes} bytes");
            }

            var nonceBytes = new byte[NonceSizeInBytes];
            RandomNumberGenerator.Fill(nonceBytes);

            var plaintextBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(walletData));
            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSizeInBytes];

            using (var aesGcm = new AesGcm(keyBytes))
                aesGcm.Encrypt(nonceBytes, plaintextBytes, ciphertext, tag);

            var encryptedData = new byte[ciphertext.Length + TagSizeInBytes];
            Array.Copy(ciphertext, 0, encryptedData, 0, ciphertext.Length);
            Array.Copy(tag, 0, encryptedData, ciphertext.Length, TagSizeInBytes);

            Array.Clear(keyBytes, 0, keyBytes.Length);
            Array.Clear(plaintextBytes, 0, plaintextBytes.Length);

            return Result.Success(new EncryptedWallet
            {
                Id = id,
                Name = name,
                IV = Convert.ToBase64String(nonceBytes),
                EncryptedData = Convert.ToBase64String(encryptedData),
                Salt = string.Empty
            });
        }
        catch (FormatException ex)
        {
            return Result.Failure<EncryptedWallet>($"Invalid master key format: {ex.Message}");
        }
        catch (CryptographicException ex)
        {
            return Result.Failure<EncryptedWallet>($"Encryption failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<EncryptedWallet>($"Error encrypting wallet: {ex.Message}");
        }
    }

    public async Task<Result<WalletData>> DecryptAsync(
        EncryptedWallet encryptedWallet,
        string masterKey)
    {
        if (encryptedWallet == null)
            return Result.Failure<WalletData>("Encrypted wallet cannot be null");
        if (string.IsNullOrWhiteSpace(masterKey))
            return Result.Failure<WalletData>("Master key cannot be empty");

        try
        {
            var encryptedDataBytes = Convert.FromBase64String(encryptedWallet.EncryptedData);
            var nonceBytes = Convert.FromBase64String(encryptedWallet.IV);
            var keyBytes = Convert.FromBase64String(masterKey);

            if (keyBytes.Length != KeySizeInBytes)
            {
                Array.Clear(keyBytes, 0, keyBytes.Length);
                return Result.Failure<WalletData>($"Invalid master key size. Expected {KeySizeInBytes} bytes");
            }

            if (encryptedDataBytes.Length < TagSizeInBytes)
                return Result.Failure<WalletData>("Encrypted data is too short");

            var ciphertextLength = encryptedDataBytes.Length - TagSizeInBytes;
            var ciphertext = new byte[ciphertextLength];
            var tag = new byte[TagSizeInBytes];
            Array.Copy(encryptedDataBytes, 0, ciphertext, 0, ciphertextLength);
            Array.Copy(encryptedDataBytes, ciphertextLength, tag, 0, TagSizeInBytes);

            var decryptedBytes = new byte[ciphertextLength];
            using (var aesGcm = new AesGcm(keyBytes))
                aesGcm.Decrypt(nonceBytes, ciphertext, tag, decryptedBytes);

            Array.Clear(keyBytes, 0, keyBytes.Length);

            var walletData = JsonSerializer.Deserialize<WalletData>(Encoding.UTF8.GetString(decryptedBytes));
            Array.Clear(decryptedBytes, 0, decryptedBytes.Length);

            return walletData == null
                ? Result.Failure<WalletData>("Failed to deserialize wallet data")
                : Result.Success(walletData);
        }
        catch (CryptographicException ex)
        {
            return Result.Failure<WalletData>($"Decryption failed — wrong key or tampered data: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure<WalletData>($"Error decrypting wallet: {ex.Message}");
        }
    }

    public async Task<Result<EncryptedWallet>> EncryptWithStoredKeyAsync(
        WalletData walletData,
        string walletId,
        string name)
    {
        // Reuse existing key on retry, generate fresh one on first call
        var hasKey = await _secureStorage.HasMasterKeyAsync(walletId);
        if (hasKey.IsFailure)
            return Result.Failure<EncryptedWallet>($"Failed to check master key: {hasKey.Error}");

        string masterKey;
        if (hasKey.Value)
        {
            var getResult = await _secureStorage.GetMasterKeyAsync(walletId);
            if (getResult.IsFailure)
                return Result.Failure<EncryptedWallet>($"Failed to retrieve master key: {getResult.Error}");
            masterKey = getResult.Value;
            Console.WriteLine($"[SecureStorage] Reusing existing master key for wallet: {walletId}");
        }
        else
        {
            var storeResult = await _secureStorage.StoreMasterKeyAsync(walletId);
            if (storeResult.IsFailure)
                return Result.Failure<EncryptedWallet>($"Failed to store master key: {storeResult.Error}");
            masterKey = storeResult.Value;
            Console.WriteLine($"[SecureStorage] New master key stored for wallet: {walletId}");
        }

        return await EncryptAsync(walletData, masterKey, name, walletId);
    }

    public async Task<Result<WalletData>> DecryptWithStoredKeyAsync(EncryptedWallet encryptedWallet)
    {
        if (encryptedWallet == null)
            return Result.Failure<WalletData>("Encrypted wallet cannot be null");

        var masterKeyResult = await _secureStorage.GetMasterKeyAsync(encryptedWallet.Id);
        if (masterKeyResult.IsFailure)
            return Result.Failure<WalletData>($"Failed to retrieve master key: {masterKeyResult.Error}");

        Console.WriteLine($"[SecureStorage] Master key retrieved for wallet: {encryptedWallet.Id}");

        return await DecryptAsync(encryptedWallet, masterKeyResult.Value);
    }
}
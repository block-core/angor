using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class AesWalletEncryption : IWalletEncryption
{
    private const int Iterations = 100000;
    private const int KeySize = 256;

    public async Task<Result<WalletData>> Decrypt(EncryptedWallet encryptedWallet, string encryptionKey)
    {
        try
        {
            var salt = Convert.FromBase64String(encryptedWallet.Salt);
            var encryptedData = Convert.FromBase64String(encryptedWallet.EncryptedData);
            var iv = Convert.FromBase64String(encryptedWallet.IV);

            using var deriveBytes = new Rfc2898DeriveBytes(
                encryptionKey,
                salt,
                Iterations,
                HashAlgorithmName.SHA256);
            var key = deriveBytes.GetBytes(KeySize / 8);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var msDecrypt = new MemoryStream(encryptedData);
            using var csDecrypt = new CryptoStream(msDecrypt, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(csDecrypt);
            var jsonData = await reader.ReadToEndAsync();
            return Result.Success(JsonSerializer.Deserialize<WalletData>(jsonData)!);
        }
        catch (Exception ex)
        {
            return Result.Failure<WalletData>($"Error decrypting wallet: {ex.Message}");
        }
    }

    public async Task<EncryptedWallet> Encrypt(WalletData walletData, string encryptionKey, string name, string id)
    {
        var salt = GenerateRandomBytes(32);
        var iv = GenerateRandomBytes(16);

        using var deriveBytes = new Rfc2898DeriveBytes(
            encryptionKey,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);
        var key = deriveBytes.GetBytes(KeySize / 8);

        byte[] encryptedData;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            using var msEncrypt = new MemoryStream();
            await using (var csEncrypt = new CryptoStream(msEncrypt, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                await JsonSerializer.SerializeAsync(csEncrypt, walletData);
            }

            encryptedData = msEncrypt.ToArray();
        }

        return new EncryptedWallet
        {
            Id = id,
            Salt = Convert.ToBase64String(salt),
            IV = Convert.ToBase64String(iv),
            EncryptedData = Convert.ToBase64String(encryptedData)
        };
    }

    private static byte[] GenerateRandomBytes(int length)
    {
        var randomBytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return randomBytes;
    }
}
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Angor.Shared;
using Angor.Shared.Models; // For NetworkConfiguration

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class ProtectedDataWalletEncryption(NetworkConfiguration _networkConfig, IWalletOperations  walletOperations) : IWalletEncryption
{

    public async Task<Result<WalletData>> Decrypt(EncryptedWallet encryptedWallet, string _)
    {
        try
        {
            var encryptedData = Convert.FromBase64String(encryptedWallet.EncryptedData);
            var decryptedBytes = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);

            using var ms = new MemoryStream(decryptedBytes);
            var walletData = await JsonSerializer.DeserializeAsync<WalletData>(ms);
            return Result.Success(walletData!);
        }
        catch (Exception ex)
        {
            return Result.Failure<WalletData>($"Error decrypting wallet: {ex.Message}");
        }
    }

    public async Task<EncryptedWallet> Encrypt(WalletData walletData, string _, string name)
    {
        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, walletData);
        var plainBytes = ms.ToArray();

        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        var walletWords = new WalletWords { Words = walletData.SeedWords };
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        var walletId = HashXpub(accountInfo.RootExtPubKey);

        return new EncryptedWallet
        {
            Id = walletId,
            Salt = string.Empty, // Not used
            IV = string.Empty,   // Not used
            EncryptedData = Convert.ToBase64String(encryptedBytes)
        };
    }

    private static string? ExtractMasterPubKey(string? descriptorJson)
    {
        if (string.IsNullOrEmpty(descriptorJson))
            return null;
        // Regex for xpub/tpub/vpub/etc. (BIP32 extended pubkeys)
        var match = Regex.Match(descriptorJson, @"[xtv]pub[a-zA-Z0-9]{100,}");
        return match.Success ? match.Value : null;
    }

    private static string HashXpub(string xpub)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(xpub));
        // Return as hex string
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}

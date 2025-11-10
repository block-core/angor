using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class FilePasswordProvider : IPasswordProvider
{
    private readonly ISecureStorage secureStorage;
    private readonly IApplicationStorage appStorage;
    private readonly string passwordDirectory;
    private readonly ILogger<FilePasswordProvider> logger;

    public FilePasswordProvider(ISecureStorage secureStorage, IApplicationStorage appStorage, string appName, string profileName, ILogger<FilePasswordProvider> logger)
    {
        this.secureStorage = secureStorage;
        this.appStorage = appStorage;
        this.logger = logger;
        // Use IApplicationStorage to get the profile directory for storing passwords directly
        this.passwordDirectory = appStorage.GetProfileDirectory(appName, profileName);
        // Directory.CreateDirectory is not needed; GetProfileDirectory already ensures existence
    }

    public async Task<Maybe<string>> Get(WalletId walletId)
    {
        var filePath = Path.Combine(passwordDirectory, $"{walletId.Value}.pwd");
        if (File.Exists(filePath))
        {
            var encrypted = await File.ReadAllTextAsync(filePath);
            var decryptedResult = secureStorage.Decrypt(encrypted);
            if (decryptedResult.IsSuccess)
            {
                return Maybe<string>.From(decryptedResult.Value);
            }

            logger.LogWarning("Failed to decrypt password for wallet {WalletId}: {Error}", walletId.Value, decryptedResult.Error);
            return Maybe<string>.None;
        }

        // Generate a secure random password (32 bytes, Base64-encoded)
        var passwordBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(passwordBytes);
        }
        var password = Convert.ToBase64String(passwordBytes);
        var encryptedResult = secureStorage.Encrypt(password);
        if (!encryptedResult.IsSuccess)
        {
            logger.LogWarning("Failed to encrypt password for wallet {WalletId}: {Error}", walletId.Value, encryptedResult.Error);
            return Maybe<string>.None;
        }
        await File.WriteAllTextAsync(filePath, encryptedResult.Value);
        return Maybe<string>.From(password);
    }
}

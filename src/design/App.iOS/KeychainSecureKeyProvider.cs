using System.Security.Cryptography;
using System.Text;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Foundation;
using Security;

namespace App.iOS;

/// <summary>
/// iOS ISecureKeyProvider backed by the Apple Keychain. Each per-wallet encryption
/// key is stored as a separate generic-password item, scoped by a profile-specific
/// service name. Keys are protected with WhenUnlockedThisDeviceOnly — they are only
/// accessible while the device is unlocked and are never included in backups.
/// </summary>
public class KeychainSecureKeyProvider : ISecureKeyProvider
{
    private readonly string _service;

    public KeychainSecureKeyProvider(IApplicationStorage storage, ProfileContext profileContext)
    {
        _service = $"{profileContext.AppName}-{profileContext.ProfileName}-WalletKeys";
    }

    public Task<Maybe<string>> Get(WalletId walletId)
    {
        var query = new SecRecord(SecKind.GenericPassword)
        {
            Service = _service,
            Account = walletId.Value,
        };

        var status = SecKeyChain.QueryAsData(query, false, out var data);

        if (status == SecStatusCode.Success && data != null)
        {
            var key = NSString.FromData(data, NSStringEncoding.UTF8)?.ToString();
            return Task.FromResult(key != null
                ? Maybe<string>.From(key)
                : Maybe<string>.None);
        }

        return Task.FromResult(Maybe<string>.None);
    }

    public Task Save(WalletId walletId, string key)
    {
        var existing = new SecRecord(SecKind.GenericPassword)
        {
            Service = _service,
            Account = walletId.Value,
        };

        var status = SecKeyChain.QueryAsData(existing, false, out _);

        if (status == SecStatusCode.Success)
        {
            var update = new SecRecord
            {
                ValueData = NSData.FromString(key, NSStringEncoding.UTF8),
            };
            SecKeyChain.Update(existing, update);
        }
        else
        {
            var record = new SecRecord(SecKind.GenericPassword)
            {
                Service = _service,
                Account = walletId.Value,
                ValueData = NSData.FromString(key, NSStringEncoding.UTF8),
                Accessible = SecAccessible.WhenUnlockedThisDeviceOnly,
            };
            SecKeyChain.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task Remove(WalletId walletId)
    {
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = _service,
            Account = walletId.Value,
        };
        SecKeyChain.Remove(record);
        return Task.CompletedTask;
    }

    public string GenerateKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

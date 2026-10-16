using Angor.Sdk.WalletExport.Blossom;
using Angor.Sdk.WalletExport.Crypto;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.WalletExport;

public sealed class BackupRecoveryService : IBackupRecoveryService
{
    private readonly INetworkConfiguration networkConfiguration;
    private readonly IBackupBlossomClient blossomClient;
    private readonly IRelayService relayService;
    private readonly ILogger<BackupRecoveryService> logger;

    public BackupRecoveryService(
        INetworkConfiguration networkConfiguration,
        IBackupBlossomClient blossomClient,
        IRelayService relayService,
        ILogger<BackupRecoveryService> logger)
    {
        this.networkConfiguration = networkConfiguration;
        this.blossomClient = blossomClient;
        this.relayService = relayService;
        this.logger = logger;
    }

    public async Task<Result<BackupRecoveryResult>> RecoverAsync(string recoveryPassphrase, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recoveryPassphrase))
            return Result.Failure<BackupRecoveryResult>("Recovery passphrase is required.");

        using var keys = BackupKeys.FromPassphrase(recoveryPassphrase);

        var outerCipher = await relayService.FetchAppSpecificDataAsync(keys.BackupPublicKeyHex, BackupManifest.DTag);
        if (string.IsNullOrWhiteSpace(outerCipher))
            return Result.Failure<BackupRecoveryResult>("No backup found for this passphrase on the configured relays.");

        BackupManifest manifest;
        try
        {
            manifest = BackupEnvelope.DecryptOuterManifest(outerCipher, keys);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt manifest — passphrase likely wrong or event tampered.");
            return Result.Failure<BackupRecoveryResult>("Could not decrypt the backup manifest. Check the passphrase.");
        }

        var candidates = manifest.Servers
            .Concat(networkConfiguration.GetDefaultBackupServerUrls().Select(s => s.Url))
            .Select(u => u.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        byte[]? innerCipher = null;
        string? servedFrom = null;
        foreach (var server in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var download = await blossomClient.DownloadAsync(server, manifest.BlobSha256, cancellationToken);
            if (download.IsSuccess)
            {
                innerCipher = download.Value;
                servedFrom = server;
                break;
            }
            logger.LogInformation("Recovery: blob unavailable at {Server}: {Reason}", server, download.Error);
        }

        if (innerCipher is null || servedFrom is null)
            return Result.Failure<BackupRecoveryResult>(
                $"Backup blob {manifest.BlobSha256} not retrievable from any of {candidates.Count} Blossom servers.");

        BackupSeedPayload payload;
        try
        {
            payload = BackupEnvelope.DecryptInner(innerCipher, keys);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inner AEAD decryption failed — possible tamper.");
            return Result.Failure<BackupRecoveryResult>("Backup blob failed authenticity check.");
        }

        return Result.Success(new BackupRecoveryResult(payload, servedFrom));
    }
}

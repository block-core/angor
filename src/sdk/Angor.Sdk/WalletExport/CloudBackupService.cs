using System.Security.Cryptography;
using Angor.Sdk.WalletExport.Blossom;
using Angor.Sdk.WalletExport.Crypto;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Nostr.Client.Responses;

namespace Angor.Sdk.WalletExport;

public sealed class CloudBackupService : ICloudBackupService
{
    private readonly INetworkConfiguration networkConfiguration;
    private readonly IBackupBlossomClient blossomClient;
    private readonly IRelayService relayService;
    private readonly ILogger<CloudBackupService> logger;

    public CloudBackupService(
        INetworkConfiguration networkConfiguration,
        IBackupBlossomClient blossomClient,
        IRelayService relayService,
        ILogger<CloudBackupService> logger)
    {
        this.networkConfiguration = networkConfiguration;
        this.blossomClient = blossomClient;
        this.relayService = relayService;
        this.logger = logger;
    }

    public async Task<Result<(BackupCreationResult Result, CloudBackupRecord Record)>> CreateBackupAsync(
        string recoveryPassphrase, BackupSeedPayload payload, int minServerSuccessThreshold = 2,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recoveryPassphrase))
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("Recovery passphrase is required.");
        if (payload is null)
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("Seed payload is required.");
        if (string.IsNullOrWhiteSpace(payload.Mnemonic))
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("Mnemonic is required in payload.");

        using var keys = BackupKeys.FromPassphrase(recoveryPassphrase);

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        payload.CreatedAtUnix = nowUnix;

        byte[] innerCipher;
        try
        {
            innerCipher = BackupEnvelope.EncryptInner(payload, keys);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inner encryption of backup payload failed.");
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("Failed to encrypt seed payload.");
        }

        var blobSha256 = Convert.ToHexString(SHA256.HashData(innerCipher)).ToLowerInvariant();
        var servers = networkConfiguration
            .GetDefaultBackupServerUrls()
            .Select(s => s.Url.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (servers.Count == 0)
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("No backup servers configured.");

        var uploadTasks = servers.Select(async server =>
        {
            var r = await blossomClient.UploadAsync(server, innerCipher, blobSha256, keys.BackupPrivateKeyHex, cancellationToken);
            return (Server: server, Result: r);
        }).ToList();

        var uploadResults = await Task.WhenAll(uploadTasks);

        var succeeded = uploadResults.Where(t => t.Result.IsSuccess).Select(t => t.Server).ToList();
        var failed = uploadResults.Where(t => t.Result.IsFailure).Select(t => t.Server).ToList();

        if (succeeded.Count < minServerSuccessThreshold)
        {
            var failedDetails = string.Join("; ", uploadResults.Where(t => t.Result.IsFailure).Select(t => $"{t.Server}: {t.Result.Error}"));
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>(
                $"Only {succeeded.Count} of {servers.Count} Blossom servers accepted the blob (needed {minServerSuccessThreshold}). {failedDetails}");
        }

        var manifest = new BackupManifest
        {
            BlobSha256 = blobSha256,
            BlobSize = innerCipher.Length,
            Servers = succeeded,
            CreatedAtUnix = nowUnix,
            Label = payload.Label
        };

        string outerCipher;
        try
        {
            outerCipher = BackupEnvelope.EncryptOuterManifest(manifest, keys);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outer NIP-44 encryption of manifest failed.");
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("Failed to encrypt backup manifest.");
        }

        try
        {
            await PublishManifestAsync(outerCipher, keys.BackupPrivateKeyHex, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Publishing manifest event to relays failed.");
            return Result.Failure<(BackupCreationResult, CloudBackupRecord)>("Failed to publish backup manifest to relays.");
        }

        var record = new CloudBackupRecord
        {
            BackupPubKeyHex = keys.BackupPublicKeyHex,
            BlobSha256 = blobSha256,
            Servers = succeeded,
            ManifestCipherText = outerCipher,
            CreatedAtUnix = nowUnix,
            LastVerifiedAtUnix = nowUnix,
            ServerHealth = succeeded.ToDictionary(s => s, _ => true, StringComparer.OrdinalIgnoreCase)
        };

        var result = new BackupCreationResult(keys.BackupPublicKeyHex, blobSha256, succeeded, failed, nowUnix);
        return Result.Success((result, record));
    }

    private async Task PublishManifestAsync(string outerCipher, string nsecHex, CancellationToken cancellationToken)
    {
        var ackTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => ackTcs.TrySetCanceled());

        Action<NostrOkResponse> onAck = ok =>
        {
            if (ok.Accepted) ackTcs.TrySetResult(true);
        };

        await relayService.PublishAppSpecificDataAsync(BackupManifest.DTag, outerCipher, nsecHex, onAck);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        timeoutCts.Token.Register(() => ackTcs.TrySetResult(false));

        await ackTcs.Task;
    }
}

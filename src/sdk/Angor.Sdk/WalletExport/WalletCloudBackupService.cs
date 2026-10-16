using Angor.Sdk.WalletExport.Blossom;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.WalletExport;

public sealed class WalletCloudBackupService : IWalletCloudBackupService
{
    private readonly IWalletAppService walletAppService;
    private readonly IWalletStore walletStore;
    private readonly ICloudBackupService cloudBackupService;
    private readonly IBackupBlossomClient blossomClient;
    private readonly INetworkConfiguration networkConfiguration;
    private readonly ILogger<WalletCloudBackupService> logger;

    public WalletCloudBackupService(
        IWalletAppService walletAppService,
        IWalletStore walletStore,
        ICloudBackupService cloudBackupService,
        IBackupBlossomClient blossomClient,
        INetworkConfiguration networkConfiguration,
        ILogger<WalletCloudBackupService> logger)
    {
        this.walletAppService = walletAppService;
        this.walletStore = walletStore;
        this.cloudBackupService = cloudBackupService;
        this.blossomClient = blossomClient;
        this.networkConfiguration = networkConfiguration;
        this.logger = logger;
    }

    public async Task<Result<Maybe<CloudBackupRecord>>> GetStatus(WalletId walletId)
    {
        var listResult = await walletStore.GetAll();
        if (listResult.IsFailure)
            return Result.Failure<Maybe<CloudBackupRecord>>(listResult.Error);

        var wallet = listResult.Value.FirstOrDefault(w => w.Id == walletId.Value);
        if (wallet is null)
            return Result.Failure<Maybe<CloudBackupRecord>>($"Wallet {walletId.Value} not found.");

        return Result.Success(wallet.CloudBackup is null
            ? Maybe<CloudBackupRecord>.None
            : Maybe<CloudBackupRecord>.From(wallet.CloudBackup));
    }

    public async Task<Result<BackupCreationResult>> EnableAsync(
        WalletId walletId, string recoveryPassphrase, string label, CancellationToken cancellationToken = default)
    {
        var payloadResult = await BuildPayload(walletId, label);
        if (payloadResult.IsFailure)
            return Result.Failure<BackupCreationResult>(payloadResult.Error);

        var createResult = await cloudBackupService.CreateBackupAsync(recoveryPassphrase, payloadResult.Value, cancellationToken: cancellationToken);
        if (createResult.IsFailure)
            return Result.Failure<BackupCreationResult>(createResult.Error);

        var persistResult = await PersistRecord(walletId, createResult.Value.Record);
        if (persistResult.IsFailure)
            return Result.Failure<BackupCreationResult>(persistResult.Error);

        return Result.Success(createResult.Value.Result);
    }

    public async Task<Result> DisableAsync(WalletId walletId)
    {
        var listResult = await walletStore.GetAll();
        if (listResult.IsFailure)
            return Result.Failure(listResult.Error);

        var wallets = listResult.Value.ToList();
        var wallet = wallets.FirstOrDefault(w => w.Id == walletId.Value);
        if (wallet is null)
            return Result.Failure($"Wallet {walletId.Value} not found.");
        if (wallet.CloudBackup is null)
            return Result.Success();

        // Note: we do NOT delete the relay event or Blossom blobs here. Without the passphrase
        // we cannot sign delete events; the data on the network will be naturally pruned over time.
        // We only clear local pointers so the app no longer treats this wallet as backed-up.
        wallet.CloudBackup = null;
        return await walletStore.SaveAll(wallets);
    }

    public async Task<Result<BackupCreationResult>> RefreshAsync(
        WalletId walletId, string recoveryPassphrase, CancellationToken cancellationToken = default)
    {
        var status = await GetStatus(walletId);
        if (status.IsFailure)
            return Result.Failure<BackupCreationResult>(status.Error);
        if (status.Value.HasNoValue)
            return Result.Failure<BackupCreationResult>("Cloud backup is not enabled for this wallet.");

        // Refresh re-runs the whole encrypt/upload/publish path. The deterministic key derivation
        // means the blob hash will match if the seed has not changed — Blossom servers will accept
        // a duplicate PUT (no-op) and the kind 30078 event will be replaced by the relays' replaceable semantics.
        var label = status.Value.Value.ManifestCipherText is { Length: > 0 } ? status.Value.Value.BackupPubKeyHex : string.Empty;
        return await EnableAsync(walletId, recoveryPassphrase, label, cancellationToken);
    }

    public async Task<Result<BackupHealthResult>> VerifyHealthAsync(WalletId walletId, CancellationToken cancellationToken = default)
    {
        var status = await GetStatus(walletId);
        if (status.IsFailure)
            return Result.Failure<BackupHealthResult>(status.Error);
        if (status.Value.HasNoValue)
            return Result.Failure<BackupHealthResult>("Cloud backup is not enabled for this wallet.");

        var record = status.Value.Value;
        var servers = record.Servers
            .Concat(networkConfiguration.GetDefaultBackupServerUrls().Select(s => s.Url))
            .Select(u => u.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var probes = servers.Select(async s =>
        {
            var probe = await blossomClient.ExistsAsync(s, record.BlobSha256, cancellationToken);
            return (Server: s, Reachable: probe.IsSuccess && probe.Value);
        });
        var results = await Task.WhenAll(probes);

        record.ServerHealth = results.ToDictionary(r => r.Server, r => r.Reachable, StringComparer.OrdinalIgnoreCase);
        record.LastVerifiedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var persist = await PersistRecord(walletId, record);
        if (persist.IsFailure)
            logger.LogWarning("Failed to persist updated backup health: {Error}", persist.Error);

        var reachable = results.Count(r => r.Reachable);
        return Result.Success(new BackupHealthResult(
            RelayManifestPublished: false,
            ServersReachable: reachable,
            ServersChecked: results.Length,
            RehealedServers: Array.Empty<string>(),
            CheckedAtUnix: record.LastVerifiedAtUnix.Value));
    }

    private async Task<Result<BackupSeedPayload>> BuildPayload(WalletId walletId, string label)
    {
        var seedResult = await walletAppService.GetSeedWords(walletId);
        if (seedResult.IsFailure)
            return Result.Failure<BackupSeedPayload>(seedResult.Error);

        var network = networkConfiguration.GetNetwork()?.Name ?? string.Empty;

        return Result.Success(new BackupSeedPayload
        {
            WalletId = walletId.Value,
            Mnemonic = seedResult.Value,
            Network = network,
            Label = label ?? string.Empty,
            Bip39Passphrase = string.Empty
        });
    }

    private async Task<Result> PersistRecord(WalletId walletId, CloudBackupRecord record)
    {
        var listResult = await walletStore.GetAll();
        if (listResult.IsFailure)
            return Result.Failure(listResult.Error);

        var wallets = listResult.Value.ToList();
        var wallet = wallets.FirstOrDefault(w => w.Id == walletId.Value);
        if (wallet is null)
            return Result.Failure($"Wallet {walletId.Value} not found.");

        wallet.CloudBackup = record;
        return await walletStore.SaveAll(wallets);
    }
}

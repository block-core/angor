using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using Nostr.Client.Utils;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

public class LogExportService : ILogExportService
{
    private readonly INetworkStorage _networkStorage;
    private readonly BlossomUploadService _blossomUploadService;
    private readonly ISeedwordsProvider _seedwordsProvider;
    private readonly IDerivationOperations _derivationOperations;
    private readonly IEncryptionService _encryptionService;
    private readonly IRelayService _relayService;
    private readonly ILogger<LogExportService> _logger;

    public LogExportService(
        INetworkStorage networkStorage,
        BlossomUploadService blossomUploadService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IEncryptionService encryptionService,
        IRelayService relayService,
        ILogger<LogExportService> logger)
    {
        _networkStorage = networkStorage;
        _blossomUploadService = blossomUploadService;
        _seedwordsProvider = seedwordsProvider;
        _derivationOperations = derivationOperations;
        _encryptionService = encryptionService;
        _relayService = relayService;
        _logger = logger;
    }

    public async Task<Result> ExportAndSendAsync(string walletId, CancellationToken ct = default)
    {
        // 1. Read support npub from build-time config
        var supportNpubHex = GetSupportNpubHex();
        if (string.IsNullOrEmpty(supportNpubHex))
            return Result.Failure("Support npub is not configured");

        // 2. Collect and zip the latest log file
        var zipResult = await CreateLogZipAsync(ct);
        if (zipResult.IsFailure)
            return Result.Failure(zipResult.Error);

        var zipBytes = zipResult.Value;
        _logger.LogInformation("Log zip created, {Size} bytes", zipBytes.Length);

        // 3. Derive support DM key from wallet
        var seedResult = await _seedwordsProvider.GetSensitiveData(walletId);
        if (seedResult.IsFailure)
            return Result.Failure($"Cannot access wallet keys: {seedResult.Error}");

        var (words, passphrase) = seedResult.Value;
        var walletWords = new WalletWords
        {
            Words = words,
            Passphrase = passphrase.HasValue ? passphrase.Value : null
        };

        var supportDmKey = _derivationOperations.DeriveSupportDmKey(walletWords);
        var supportDmKeyHex = Encoders.Hex.EncodeData(supportDmKey.ToBytes());

        // 4. Encrypt the zip using NIP-04 (ECDH shared secret with support npub)
        var zipBase64 = Convert.ToBase64String(zipBytes);
        var encryptedBlob = await _encryptionService.EncryptNostrContentAsync(
            supportDmKeyHex, supportNpubHex, zipBase64);
        var encryptedBytes = System.Text.Encoding.UTF8.GetBytes(encryptedBlob);

        // 5. Upload encrypted blob to Blossom
        var uploadResult = await UploadToBlossom(encryptedBytes, ct);
        if (uploadResult.IsFailure)
            return Result.Failure(uploadResult.Error);

        var blobUrl = uploadResult.Value;
        _logger.LogInformation("Encrypted log blob uploaded to {Url}", blobUrl);

        // 6. Build and send encrypted DM with metadata
        var dmContent = BuildDmContent(blobUrl);
        var encryptedDm = await _encryptionService.EncryptNostrContentAsync(
            supportDmKeyHex, supportNpubHex, dmContent);

        var sendResult = await SendDmAsync(supportDmKeyHex, supportNpubHex, encryptedDm, ct);
        if (sendResult.IsFailure)
            return Result.Failure(sendResult.Error);

        _logger.LogInformation("Log export DM sent to support");
        return Result.Success();
    }

    private static string? GetSupportNpubHex()
    {
        var assembly = typeof(LogExportService).Assembly;
        var attr = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SupportNpub");

        var npub = attr?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(npub))
            return null;

        // If already hex (64 chars), use directly
        if (npub.Length == 64 && npub.All(c => "0123456789abcdef".Contains(c)))
            return npub;

        // Convert bech32 npub to hex using Nostr.Client
        if (npub.StartsWith("npub1"))
        {
            return NostrConverter.ToHex(npub, out _);
        }

        return null;
    }

    private async Task<Result<byte[]>> CreateLogZipAsync(CancellationToken ct)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        if (string.IsNullOrWhiteSpace(localAppData))
            return Result.Failure<byte[]>("Cannot determine application data directory");

        var logsDir = Path.Combine(
            localAppData,
            "Angor", "logs");

        if (!Directory.Exists(logsDir))
            return Result.Failure<byte[]>("No log directory found");

        var logFiles = Directory.GetFiles(logsDir, "*.log");
        if (logFiles.Length == 0)
            return Result.Failure<byte[]>("No log files found");

        // Pick the most recently modified log file
        var latestLog = logFiles
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .First();

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(latestLog.Name, CompressionLevel.SmallestSize);
            await using var entryStream = entry.Open();

            // Open with ReadWrite share since Serilog holds the file open
            await using var fileStream = new FileStream(
                latestLog.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await fileStream.CopyToAsync(entryStream, ct);
        }

        return Result.Success(ms.ToArray());
    }

    private async Task<Result<string>> UploadToBlossom(byte[] encryptedBytes, CancellationToken ct)
    {
        var settings = _networkStorage.GetSettings();
        var servers = settings.ImageServers
            .OrderByDescending(s => s.IsPrimary)
            .ToList();

        if (servers.Count == 0)
            return Result.Failure<string>("No Blossom servers configured");

        var errors = new List<string>();
        foreach (var server in servers)
        {
            var result = await _blossomUploadService.UploadAsync(
                server.Url, encryptedBytes, "application/octet-stream", cancellationToken: ct);

            if (result.IsSuccess)
                return result;

            var error = $"{server.Url}: {result.Error}";
            errors.Add(error);
            _logger.LogWarning("Blossom upload to {Server} failed: {Error}", server.Url, result.Error);
        }

        return Result.Failure<string>($"All Blossom servers failed. {string.Join("; ", errors)}");
    }

    private string BuildDmContent(string blobUrl)
    {
        var version = GetAppVersion();
        var network = _networkStorage.GetNetwork() ?? "Unknown";
        var platform = RuntimeInformation.OSDescription;

        return string.Join('\n',
            "Log Export",
            $"URL: {blobUrl}",
            $"Version: {version}",
            $"Platform: {platform}",
            $"Network: {network}",
            $"Timestamp: {DateTime.UtcNow:O}");
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(LogExportService).Assembly;
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attr != null)
        {
            var ver = attr.InformationalVersion;
            var plusIndex = ver.IndexOf('+');
            return plusIndex >= 0 ? ver[..plusIndex] : ver;
        }

        var version = assembly.GetName().Version;
        return version != null ? version.ToString(3) : "0.0.0";
    }

    private Task<Result> SendDmAsync(string senderKeyHex, string recipientPubKeyHex,
        string encryptedContent, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => tcs.TrySetResult(false));

        _relayService.SendDirectMessagesForPubKeyAsync(
            senderKeyHex, recipientPubKeyHex, encryptedContent,
            result => tcs.TrySetResult(result.Accepted));

        return tcs.Task.ContinueWith(t =>
            t.Result ? Result.Success() : Result.Failure("DM send failed or timed out"),
            TaskScheduler.Default);
    }
}

using System.Collections.Concurrent;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin.DataEncoders;
using Nostr.Client.Messages;

namespace Angor.Shared.Services;

/// <summary>
/// Stores the investor's investments list on Nostr as an encrypted self-DM from a
/// deterministic "storage account" derived from the wallet seed words.
/// This is the single mechanism shared by the web app and the desktop/SDK app so a
/// wallet imported on either surface discovers investments made on the other.
/// The payload is an opaque serialized string; callers own the (JSON-compatible) model.
/// </summary>
public interface INostrInvestmentStorageService
{
    /// <summary>Encrypts and publishes the serialized investments payload to the relays.</summary>
    Task<Result> SaveAsync(WalletWords words, string serializedInvestments);

    /// <summary>
    /// Fetches, decrypts and returns the newest decryptable investments payload from the
    /// relays, or null when none exists. Stale/undecryptable payloads are skipped.
    /// </summary>
    Task<Result<string?>> LoadAsync(WalletWords words);
}

public class NostrInvestmentStorageService(
    IDerivationOperations derivationOperations,
    IEncryptionService encryptionService,
    IRelayService relayService,
    ILogger<NostrInvestmentStorageService> logger) : INostrInvestmentStorageService
{
    private static readonly TimeSpan RelayTimeout = TimeSpan.FromSeconds(30);

    public async Task<Result> SaveAsync(WalletWords words, string serializedInvestments)
    {
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var storageKey = derivationOperations.DeriveNostrStorageKey(words);
        var storageKeyHex = Encoders.Hex.EncodeData(storageKey.ToBytes());
        var password = derivationOperations.DeriveNostrStoragePassword(words);

        var encrypted = await encryptionService.EncryptData(serializedInvestments, password);

        var tcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(RelayTimeout);
        cts.Token.Register(() => tcs.TrySetResult(false));
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted,
            result => { tcs.TrySetResult(result.Accepted); });

        var success = await tcs.Task;
        return success ? Result.Success() : Result.Failure("Failed to push investment records to relay");
    }

    public async Task<Result<string?>> LoadAsync(WalletWords words)
    {
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var password = derivationOperations.DeriveNostrStoragePassword(words);

        using var cts = new CancellationTokenSource(RelayTimeout);
        var tcs = new TaskCompletionSource<Result>();
        cts.Token.Register(() => tcs.TrySetCanceled());

        // Collect events from all relays, keyed by content to deduplicate.
        // Different relays may return the same event or different versions
        // (e.g. stale data from a previous session that used a different key).
        var receivedEvents = new ConcurrentDictionary<string, NostrEvent>();

        relayService.LookupDirectMessagesForPubKey(storageAccountKey, null, 1, nostrEvent =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(nostrEvent.Content))
                    {
                        // Keep the newest event per unique content payload
                        receivedEvents.AddOrUpdate(
                            nostrEvent.Content,
                            nostrEvent,
                            (_, existing) => nostrEvent.CreatedAt > existing.CreatedAt ? nostrEvent : existing);
                    }

                    tcs.TrySetResult(Result.Success());
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }

                return tcs.Task;
            }, new[] { storageAccountKey }, false,
            () => { tcs.TrySetResult(Result.Success()); });

        try
        {
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // Timeout — but we may have collected events before the timeout fired.
            // Fall through to process whatever we have.
        }

        return await TryDecryptRelayEvents(receivedEvents, storageAccountKey, password);
    }

    private async Task<Result<string?>> TryDecryptRelayEvents(
        ConcurrentDictionary<string, NostrEvent> receivedEvents,
        string storageAccountKey,
        string password)
    {
        // Sort unique payloads by timestamp, newest first
        var uniqueEvents = receivedEvents.Values
            .OrderByDescending(e => e.CreatedAt ?? DateTime.MinValue)
            .ToList();

        if (uniqueEvents.Count == 0)
            return Result.Success<string?>(null);

        if (uniqueEvents.Count > 1)
        {
            logger.LogWarning(
                "Received {UniqueCount} distinct relay payloads for storage key {StorageKey}. " +
                "Timestamps: {Timestamps}. Content lengths: {Lengths}. " +
                "This may indicate stale data on some relays from a previous session",
                uniqueEvents.Count,
                storageAccountKey[..12] + "...",
                string.Join(", ", uniqueEvents.Select(e => e.CreatedAt?.ToString("O") ?? "null")),
                string.Join(", ", uniqueEvents.Select(e => e.Content?.Length ?? 0)));
        }

        // Try decrypting each unique payload starting from the newest.
        // If the newest fails (e.g. stale data encrypted with a different key),
        // fall back to older payloads that may still be valid.
        for (var i = 0; i < uniqueEvents.Count; i++)
        {
            var nostrEvent = uniqueEvents[i];
            try
            {
                var decrypted = await encryptionService.DecryptData(nostrEvent.Content!, password);

                // The web (JS) decrypt shim returns an empty string on failure instead of throwing.
                if (string.IsNullOrEmpty(decrypted))
                    continue;

                if (uniqueEvents.Count > 1)
                {
                    logger.LogInformation(
                        "Successfully decrypted relay event from {Timestamp} (tried {Index} of {Total})",
                        nostrEvent.CreatedAt?.ToString("O") ?? "null",
                        i + 1,
                        uniqueEvents.Count);
                }

                return Result.Success<string?>(decrypted);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to decrypt relay event from {Timestamp} " +
                    "(content length={ContentLength}, payload {Index} of {Total}). " +
                    "This event may contain stale data encrypted with a different key",
                    nostrEvent.CreatedAt?.ToString("O") ?? "null",
                    nostrEvent.Content?.Length ?? 0,
                    i + 1,
                    uniqueEvents.Count);
            }
        }

        // All payloads failed to decrypt — report null rather than crashing the pipeline
        logger.LogError(
            "All {Count} distinct relay payloads for storage key {StorageKey} failed to decrypt. " +
            "The relay may contain stale data from a previous session that used a different encryption key",
            uniqueEvents.Count,
            storageAccountKey[..12] + "...");

        return Result.Success<string?>(null);
    }
}

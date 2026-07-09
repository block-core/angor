using System.Collections.Concurrent;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Services;
using NBitcoin;
using NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;

namespace Angor.Sdk.Funding.Investor.Domain;

public class PortfolioService(
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    ISeedwordsProvider seedwordsProvider,
    IRelayService relayService,
    IGenericDocumentCollection<InvestmentRecordsDocument> documentCollection,
    ILogger<PortfolioService> logger) : IPortfolioService
{
    public async Task<Result<InvestmentRecords>> GetByWalletId(string walletId)
    {
        // Try to get from local document collection first (no password needed)
        var localDoc = await documentCollection.FindByIdAsync(walletId);
        if (localDoc is { IsSuccess: true, Value: not null })
            return Result.Success(new InvestmentRecords(){ProjectIdentifiers = localDoc.Value.Investments});

        // Local not found — need wallet sensitive data to fetch from relay
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure<InvestmentRecords>(sensiveDataResult.Error);
        }
    
        var words = sensiveDataResult.Value.ToWalletWords();
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var password = derivationOperations.DeriveNostrStoragePassword(words);
    
        var relayResult = await GetInvestmentRecordsFromRelayAsync(storageAccountKey, password);
        if (relayResult.IsFailure)
        {
            return relayResult;
        }

        // Save to local document collection for future lookups
        var doc = new InvestmentRecordsDocument
        {
            WalletId = walletId,
            Investments = relayResult.Value?.ProjectIdentifiers.ToList() ?? []
        };
        
        await documentCollection.UpsertAsync(document => document.WalletId, doc);

        return relayResult;
    }

    public async Task<Result> AddOrUpdate(string walletId, InvestmentRecord investmentRecord)
    {
        var investmentsResult = await GetByWalletId(walletId);
        if (investmentsResult.IsFailure)
            return Result.Failure(investmentsResult.Error);
        
        var investments = investmentsResult.Value ?? new InvestmentRecords();
        var existingInvestment = investments.ProjectIdentifiers
            .FirstOrDefault(i => i.ProjectIdentifier == investmentRecord.ProjectIdentifier);
        if (existingInvestment != null)
            investments.ProjectIdentifiers.Remove(existingInvestment);
        
        investments.ProjectIdentifiers.Add(investmentRecord);
        
        // Save to local document collection for future lookups
        var doc = new InvestmentRecordsDocument
        {
            WalletId = walletId,
            Investments = investments.ProjectIdentifiers
        };
        
        var savedLocally = await documentCollection.UpsertAsync(document => document.WalletId, doc);

        var savedOnRelay = await PushInvestmentsRecordsToRelayAsync(walletId, investments);

        return savedLocally.IsSuccess || savedOnRelay.IsSuccess
            ? Result.Success()
            : Result.Failure("Failed to save investment record");
    }

    public async Task<Result> RemoveInvestmentRecordAsync(string walletId, InvestmentRecord investment)
    {
        var investmentsResult = await GetByWalletId(walletId);
        if (investmentsResult.IsFailure)
            return Result.Failure(investmentsResult.Error);

        var investments = investmentsResult.Value ?? new InvestmentRecords();
        var existingInvestment = investments.ProjectIdentifiers.FirstOrDefault(i => i.ProjectIdentifier == investment.ProjectIdentifier);

        if (existingInvestment == null)
            return Result.Success(); // Nothing to remove

        // todo: check if we have already published the trx,
        // if it was already published we should not allow removal

        investments.ProjectIdentifiers.Remove(existingInvestment);

        var doc = new InvestmentRecordsDocument
        {
            WalletId = walletId,
            Investments = investments.ProjectIdentifiers
        };

        var savedLocally = await documentCollection.UpsertAsync(document => document.WalletId, doc);

        return savedLocally.IsSuccess
            ? Result.Success()
            : Result.Failure("Failed to save investment record");
    }

    private async Task<Result<bool>> PushInvestmentsRecordsToRelayAsync(string walletId, InvestmentRecords investments)
    {
        // // Encrypt and send the investments
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return  Result.Failure<bool>(sensiveDataResult.Error);
        }

        var words = sensiveDataResult.Value.ToWalletWords();
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var storageKey = derivationOperations.DeriveNostrStorageKey(words);
        var storageKeyHex = Encoders.Hex.EncodeData(storageKey.ToBytes());
        var password = derivationOperations.DeriveNostrStoragePassword(words);
        
        var encrypted = await encryptionService.EncryptData(serializer.Serialize(investments), password);

        var tcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => tcs.TrySetResult(false));
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result => { tcs.TrySetResult(result.Accepted); });

        var success = await tcs.Task;
        return success ? Result.Success(true) : Result.Failure<bool>("Failed to push investment records to relay");
    }

    private async Task<Result<InvestmentRecords>> GetInvestmentRecordsFromRelayAsync(string storageAccountKey,
        string password)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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
            () =>
            {
                tcs.TrySetResult(Result.Success());
            });

        try
        {
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // Timeout — but we may have collected events before the timeout fired.
            // Fall through to process whatever we have.
        }

        // Process whatever events we collected (even if the relay lookup timed out)
        return await TryDecryptRelayEvents(receivedEvents, storageAccountKey, password);
    }

    private async Task<Result<InvestmentRecords>> TryDecryptRelayEvents(
        ConcurrentDictionary<string, NostrEvent> receivedEvents,
        string storageAccountKey,
        string password)
    {
        // Sort unique payloads by timestamp, newest first
        var uniqueEvents = receivedEvents.Values
            .OrderByDescending(e => e.CreatedAt ?? DateTime.MinValue)
            .ToList();

        if (uniqueEvents.Count == 0)
            return Result.Success(new InvestmentRecords());

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
                var investmentRecords = serializer.Deserialize<InvestmentRecords>(decrypted);

                if (uniqueEvents.Count > 1)
                {
                    logger.LogInformation(
                        "Successfully decrypted relay event from {Timestamp} (tried {Index} of {Total})",
                        nostrEvent.CreatedAt?.ToString("O") ?? "null",
                        i + 1,
                        uniqueEvents.Count);
                }

                return Result.Success(investmentRecords!);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.AuthenticationTagMismatchException
                                           or System.Security.Cryptography.CryptographicException
                                           or FormatException)
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

        // All payloads failed to decrypt — return empty rather than crashing the pipeline
        logger.LogError(
            "All {Count} distinct relay payloads for storage key {StorageKey} failed to decrypt. " +
            "Returning empty investment records. The relay may contain stale data " +
            "from a previous session that used a different encryption key",
            uniqueEvents.Count,
            storageAccountKey[..12] + "...");

        return Result.Success(new InvestmentRecords());
    }
}
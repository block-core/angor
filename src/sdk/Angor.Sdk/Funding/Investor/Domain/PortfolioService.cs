using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Domain;

public class PortfolioService(
    ISerializer serializer,
    ISeedwordsProvider seedwordsProvider,
    INostrInvestmentStorageService nostrInvestmentStorage,
    IGenericDocumentCollection<InvestmentRecordsDocument> documentCollection,
    ILogger<PortfolioService> logger) : IPortfolioService
{
    public async Task<Result<InvestmentRecords>> GetByWalletId(string walletId)
    {
        // Try to get from local document collection first (no password needed).
        // An empty local document is NOT authoritative: it may have been cached from a
        // failed/timed-out relay lookup, so fall through to the relay in that case.
        var localDoc = await documentCollection.FindByIdAsync(walletId);
        if (localDoc is { IsSuccess: true, Value.Investments.Count: > 0 })
            return Result.Success(new InvestmentRecords { ProjectIdentifiers = localDoc.Value.Investments });

        // Local not found (or empty) — need wallet sensitive data to fetch from relay
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure<InvestmentRecords>(sensiveDataResult.Error);
        }

        var words = sensiveDataResult.Value.ToWalletWords();

        var relayResult = await nostrInvestmentStorage.LoadAsync(words);
        if (relayResult.IsFailure)
        {
            return Result.Failure<InvestmentRecords>(relayResult.Error);
        }

        if (relayResult.Value is null)
        {
            // Nothing on the relays (or nothing decryptable). Do NOT cache the empty
            // result — a transient relay failure must not permanently hide investments.
            return Result.Success(new InvestmentRecords());
        }

        InvestmentRecords? records;
        try
        {
            records = serializer.Deserialize<InvestmentRecords>(relayResult.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize investment records payload from relay");
            return Result.Success(new InvestmentRecords());
        }

        records ??= new InvestmentRecords();

        // Save to local document collection for future lookups
        var doc = new InvestmentRecordsDocument
        {
            WalletId = walletId,
            Investments = records.ProjectIdentifiers.ToList()
        };

        await documentCollection.UpsertAsync(document => document.WalletId, doc);

        return Result.Success(records);
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

    private async Task<Result> PushInvestmentsRecordsToRelayAsync(string walletId, InvestmentRecords investments)
    {
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure(sensiveDataResult.Error);
        }

        var words = sensiveDataResult.Value.ToWalletWords();
        return await nostrInvestmentStorage.SaveAsync(words, serializer.Serialize(investments));
    }
}

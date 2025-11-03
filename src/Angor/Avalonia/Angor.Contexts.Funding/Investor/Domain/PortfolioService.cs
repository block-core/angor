using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Domain;

public class PortfolioService(
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    ISeedwordsProvider seedwordsProvider,
    IRelayService relayService,
    IGenericDocumentCollection<InvestmentRecordsDocument> documentCollection) : IPortfolioService
{
    public async Task<Result<InvestmentRecords>> GetByWalletId(Guid walletId)
    {
        // We need to pronmpt user permission to access wallet sensitive data
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure<InvestmentRecords>(sensiveDataResult.Error);
        }
        
        // Try to get from local document collection first
        var localDoc = await documentCollection.FindByIdAsync(walletId.ToString());
        if (localDoc is { IsSuccess: true, Value: not null })
            return Result.Success(new InvestmentRecords(){ProjectIdentifiers = localDoc.Value.Investments});
    
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
            WalletId = walletId.ToString(),
            Investments = relayResult.Value?.ProjectIdentifiers.ToList() ?? []
        };
        
        await documentCollection.UpsertAsync(document => document.WalletId, doc);

        return relayResult;
    }

    public async Task<Result> Add(Guid walletId, InvestmentRecord newInvestment)
    {
        var investmentsResult = await GetByWalletId(walletId);
        if (investmentsResult.IsFailure)
            return Result.Failure(investmentsResult.Error);
        
        var investments = investmentsResult.Value ?? new InvestmentRecords();
        var existingInvestment = investments.ProjectIdentifiers
            .FirstOrDefault(i => i.ProjectIdentifier == newInvestment.ProjectIdentifier);
        if (existingInvestment != null)
            investments.ProjectIdentifiers.Remove(existingInvestment);
        
        investments.ProjectIdentifiers.Add(newInvestment);
        
        // Save to local document collection for future lookups
        var doc = new InvestmentRecordsDocument
        {
            WalletId = walletId.ToString(),
            Investments = investments.ProjectIdentifiers
        };
        
        var savedLocally = await documentCollection.UpsertAsync(document => document.WalletId, doc);

        var savedOnRelay = await PushInvestmentsRecordsToRelayAsync(walletId, investments);

        return savedLocally.IsSuccess || savedOnRelay.IsSuccess
            ? Result.Success()
            : Result.Failure("Failed to save investment record");
    }

    private async Task<Result<bool>> PushInvestmentsRecordsToRelayAsync(Guid walletId, InvestmentRecords investments)
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
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result => { tcs.SetResult(result.Accepted); });

        var success = await tcs.Task;
        return success ? Result.Success(true) : Result.Failure<bool>("Failed to push investment records to relay");
    }

    private async Task<Result<InvestmentRecords>> GetInvestmentRecordsFromRelayAsync(string storageAccountKey,
        string password)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var tcs = new TaskCompletionSource<Result>();
            
        cts.Token.Register(() => tcs.TrySetCanceled());

        var encryptedContent = string.Empty;
        
        relayService.LookupDirectMessagesForPubKey(storageAccountKey, null, 1, nostrEvent =>
            {
                try
                {
                    encryptedContent = nostrEvent.Content;
                    tcs.TrySetResult( Result.Success());
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
            var lookupResult = await tcs.Task;
            
            if (!lookupResult.IsSuccess) 
                return Result.Failure<InvestmentRecords>(lookupResult.Error);

            if (string.IsNullOrEmpty(encryptedContent))
                return Result.Success(new InvestmentRecords());

            var decrypted = await encryptionService.DecryptData(encryptedContent, password);
            var investmentRecords = serializer.Deserialize<InvestmentRecords>(decrypted);
            return Result.Success(investmentRecords!);

        }
        catch (OperationCanceledException e)
        {
            return Result.Failure<InvestmentRecords>(e.Message);
        }

    }
}
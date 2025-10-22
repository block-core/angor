using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Domain.Impl;

public class PortfolioRepository(
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    ISeedwordsProvider seedwordsProvider,
    IRelayService relayService) : IPortfolioRepository
{
    public async Task<Result<InvestmentRecords>> GetByWalletId(Guid walletId)
    {
        // Encrypt and send the investments
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure<InvestmentRecords>(sensiveDataResult.Error);
        }
        var words = sensiveDataResult.Value.ToWalletWords();
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var password = derivationOperations.DeriveNostrStoragePassword(words);
        return await GetInvestmentRecordsFromRelayAsync(storageAccountKey, password);
    }

    public async Task<Result> Add(Guid walletId, InvestmentRecord newInvestment)
    {
        // Encrypt and send the investments
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure(sensiveDataResult.Error);
        }
        var words = sensiveDataResult.Value.ToWalletWords();
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var storageKey = derivationOperations.DeriveNostrStorageKey(words);
        var storageKeyHex = Encoders.Hex.EncodeData(storageKey.ToBytes());
        var password = derivationOperations.DeriveNostrStoragePassword(words);
        
        var investments = await GetInvestmentRecordsFromRelayAsync(storageAccountKey, password);

        if (investments.IsFailure)
            return Result.Failure(investments.Error);
        
        var existingInvestment =
            investments.Value.ProjectIdentifiers.FirstOrDefault(p =>
                p.ProjectIdentifier == newInvestment.ProjectIdentifier);
        
        if (existingInvestment != null)
            investments.Value.ProjectIdentifiers.Remove(existingInvestment);
        
        investments.Value.ProjectIdentifiers.Add(newInvestment);
        
        var encrypted = await encryptionService.EncryptData(serializer.Serialize(investments.Value), password);

        var tcs = new TaskCompletionSource<bool>();
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result => { tcs.SetResult(result.Accepted); });

        var success = await tcs.Task;
        return success ? Result.Success() : Result.Failure("Error adding investment");
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
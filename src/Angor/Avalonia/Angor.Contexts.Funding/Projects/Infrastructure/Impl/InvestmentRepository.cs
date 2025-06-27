using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class InvestmentRepository(
    IIndexerService indexerService,
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    ISeedwordsProvider seedwordsProvider,
    IRelayService relayService) : IInvestmentRepository
{
    public async Task<Result<InvestmentRecords>> GetByWallet(Guid walletId)
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

    public async Task<Result> Add(Guid walletId, Domain.Investment newInvestment)
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
        
        investments.Value.ProjectIdentifiers.Add(new InvestorPositionRecord
        {
            InvestmentTransactionHash = newInvestment.TransactionId,
            InvestorPubKey = newInvestment.InvestorPubKey,
            ProjectIdentifier = newInvestment.ProjectId.Value,
            UnfundedReleaseAddress = null //TODO
        });
        
        var encrypted = await encryptionService.EncryptData(serializer.Serialize(investments.Value), password);

        var tcs = new TaskCompletionSource<bool>();
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result => { tcs.SetResult(result.Accepted); });

        var success = await tcs.Task;
        return success ? Result.Success() : Result.Failure("Error adding investment");
    }

    public Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId)
    {
        return GetProjectInvestments(projectId).Map(enumerable => enumerable.Select(inv => new InvestmentDto
        {
            ProjectId = projectId,
            InvestorKey = inv.InvestorPubKey,
            AmountInSats = inv.Amount.Sats,
            TransactionId = inv.TransactionId
        }));
    }

    private Task<Result<IEnumerable<Domain.Investment>>> GetProjectInvestments(ProjectId projectId)
    {
        return Result.Try(() => indexerService.GetInvestmentsAsync(projectId.Value))
            .Map(investments => investments.Select(inv => Domain.Investment.Create(projectId, inv.InvestorPublicKey, new Amount(inv.TotalAmount), inv.TransactionId)));
    }
    
    private async Task<Result<InvestmentRecords>> GetInvestmentRecordsFromRelayAsync(string storageAccountKey,
        string password)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var tcs = new TaskCompletionSource<Result<InvestmentRecords>>();
            
        cts.Token.Register(() => tcs.TrySetCanceled());
        
        relayService.LookupDirectMessagesForPubKey(storageAccountKey, null, 1, async (nostrEvent) =>
        {
            try
            {
                var decrypted = await encryptionService.DecryptData(nostrEvent.Content, password);
                var investmentRecords = serializer.Deserialize<InvestmentRecords>(decrypted);
                tcs.SetResult(investmentRecords);
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }

        }, new[] { storageAccountKey });

        
        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure<InvestmentRecords>("Operation timed out");
        }

    }
}
using System.Collections.Concurrent;
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
    private ConcurrentDictionary<string, IList<Investment>> _investmentRecordsCache = new();
    
    public Task<Result> AddAsync(Guid walletId, Domain.Investment newInvestment)
    {
        if (_investmentRecordsCache.TryGetValue(newInvestment.ProjectId.Value, out IList<Investment>? recordList))
        {
            recordList.Add(newInvestment);
        }
        else
        {
            _investmentRecordsCache.GetOrAdd(newInvestment.ProjectId.Value, _ => new List<Investment> { newInvestment });
        }
        return Task.FromResult(Result.Success());
    }

    private async Task<Result> PublishToNostr(Guid walletId, IEnumerable<Investment> investments)
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
        
        var encrypted = await encryptionService.EncryptData(serializer.Serialize(investments), password);
        
        var tcs = new TaskCompletionSource<bool>();
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result => { tcs.SetResult(result.Accepted); });
        
        var success = await tcs.Task;
        return success 
            ? Result.Success() 
            : Result.Failure("Failed to publish investments to Nostr.");
    }

    public Task<Result<Investment?>> GetAsync(Guid walletId, ProjectId projectId)
    {
        var result = _investmentRecordsCache.TryGetValue(projectId.Value, out var value)
            ? Result.Success(value.OrderBy(x => x.InvestmentDate).LastOrDefault())
            : Result.Success<Investment?>(null);
        
        return Task.FromResult(result);
    }

    public Task<Result<IEnumerable<Investment>>> GetAllAsync(Guid walletId)
    {
        return Task.FromResult(Result.Success(_investmentRecordsCache.Keys
            .SelectMany(key => _investmentRecordsCache[key])));
    }

    public Task<Result> UpdateAsync(Guid walletId, Investment investment)
    {
        if (!_investmentRecordsCache.TryGetValue(investment.ProjectId.Value, out var recordList))
            return Task.FromResult(Result.Failure("Investment records not found for the project."));
        
        // Find the old record and replace it with the new one
        var oldRecord = recordList.FirstOrDefault(x => x.TransactionId == investment.TransactionId);
        if (oldRecord == null) 
            return Task.FromResult(Result.Failure("Investment not found for update."));
        
        recordList.Remove(oldRecord);
        recordList.Add(investment);
        return Task.FromResult(Result.Success());

    }

    public Task<Result<IEnumerable<InvestmentDto>>> GetByProjectAsync(ProjectId projectId)
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
}
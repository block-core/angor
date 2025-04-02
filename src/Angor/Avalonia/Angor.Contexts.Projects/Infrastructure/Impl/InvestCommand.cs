using Angor.Contests.CrossCutting;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Angor.Contexts.Wallet.Domain;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using Amount = Angor.Contexts.Projects.Domain.Amount;
using Domain_Amount = Angor.Contexts.Projects.Domain.Amount;

namespace Angor.Contexts.Projects.Infrastructure.Impl;

public class InvestCommand(IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestorTransactionActions investorTransactionActions,
    IInvestorKeyProvider investorKeyProvider,
    IWalletOperations walletOperations,
    Guid walletId,
    ProjectId projectId,
    Domain_Amount amount)
{
    public Task<Result> Execute()
    {
        var transactionResult = from project in projectRepository.Get(projectId)
            from investorKey in GetInvestorKey(project.FounderKey)
            from unsignedTransaction in CreateInvestmentTransaction(project, investorKey)
            from signedTransaction in SignTransaction(unsignedTransaction)
            from tx in Broadcast(signedTransaction)
            select new { tx, investorKey };

        return transactionResult.Bind(id => AddInvestment(Investment.Create(projectId, id.investorKey, amount.Sats, id.tx.Value)));
    }

    private async Task<Result<string>> GetInvestorKey(string founderKey)
    {
        var investorKeyResult = await investorKeyProvider.InvestorKey(walletId, founderKey);
        if (investorKeyResult.IsFailure)
        {
            return Result.Failure<string>(investorKeyResult.Error);
        }

        return investorKeyResult;
    }

    private Result<Transaction> CreateInvestmentTransaction(Project project, Result<string> investorKey)
    {
        var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(project.ToSharedModel(), investorKey.Value, amount.Sats));
        
        if (transactionResult.IsFailure)
        {
            return Result.Failure<Transaction>(transactionResult.Error);
        }

        return transactionResult;
    }

    private async Task<Result<TransactionInfo>> SignTransaction(Transaction transaction)
    {
        var sensitiveDataResult = await investorKeyProvider.GetSensitiveData(walletId);
        if (sensitiveDataResult.IsFailure)
        {
            return Result.Failure<TransactionInfo>(sensitiveDataResult.Error);
        }
        
        var sensitiveData = sensitiveDataResult.Value;
        var walletWords = new WalletWords()
        {
            Words = sensitiveData.Words,
            Passphrase = sensitiveData.Passphrase.GetValueOrDefault(""),
        };
        
        var accountInfo = walletOperations.BuildAccountInfoForWalletWords(sensitiveData.ToWalletWords());
        await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        
        var changeAddressResult = Result.Try(() => accountInfo.GetNextChangeReceiveAddress())
            .Ensure(s => !string.IsNullOrEmpty(s), "Change address cannot be empty");

        if (changeAddressResult.IsFailure)
        {
            return Result.Failure<TransactionInfo>(changeAddressResult.Error);
        }
        
        var changeAddress = changeAddressResult.Value!;

        var feeEstimationResult = await Result.Try(walletOperations.GetFeeEstimationAsync);
        if (feeEstimationResult.IsFailure)
        {
            return Result.Failure<TransactionInfo>("Error getting fee estimation");
        }

        var feerate = feeEstimationResult.Value.FirstOrDefault()?.FeeRate ?? 1;
        
        TransactionInfo signedTransaction = walletOperations.AddInputsAndSignTransaction(
            changeAddress, 
            transaction, 
            walletWords, 
            accountInfo, 
            feerate);
        
        return signedTransaction;
    }

    private async Task<Result<TxId>> Broadcast(TransactionInfo transaction)
    {
        // TODO: implement broadcast
        return Result.Success(new TxId("61a34ef983bf8d37e3862a0537734b942c627e0904ef4e2277b6185a7355a3c3"));
    }

    private async Task<Result> AddInvestment(Investment investment)
    {
        return await investmentRepository.Add(walletId, investment);
    }
}
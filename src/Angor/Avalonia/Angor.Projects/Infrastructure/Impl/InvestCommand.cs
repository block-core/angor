using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared.ProtocolNew;
using Angor.Wallet.Domain;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using Amount = Angor.Projects.Domain.Amount;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestCommand(IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestorTransactionActions investorTransactionActions,
    IInvestorKeyProvider investorKeyProvider,
    Guid walletId,
    ProjectId projectId,
    Amount amount)
{
    public Task<Result> Execute()
    {
        var transactionResult = from project in projectRepository.Get(projectId)
            from investorKey in GetInvestorKey(project.FounderKey)
            from unsignedTransaction in CreateInvestmentTransaction(project, investorKey)
            from signedTransaction in SignTransaction(project, unsignedTransaction)
            from tx in Broadcast(signedTransaction)
            select new { tx, investorKey };

        return transactionResult.Bind(id => Save(Investment.Create(projectId, id.investorKey, amount.Sats, id.tx.Value)));
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

    private async Task<Result<Transaction>> SignTransaction(Project project, Transaction transaction)
    {
        // TODO: implement signing
        return transaction;
    }
    
    private async Task<Result<TxId>> Broadcast(Transaction transaction)
    {
        // TODO: implement broadcast
        return Result.Success(new TxId("61a34ef983bf8d37e3862a0537734b942c627e0904ef4e2277b6185a7355a3c3"));
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

    private async Task<Result> Save(Investment investment)
    {
        return await investmentRepository.Save(investment);
    }
}
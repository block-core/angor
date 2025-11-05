using Angor.Contests.CrossCutting;
using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class ClaimEndOfProject
{
    public record ClaimEndOfProjectRequest(Guid WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<EndOfProjectTransactionDraft>>;
    
    public class ClaimEndOfProjectHandler(
        IDerivationOperations derivationOperations, IProjectService projectService, 
        IInvestorTransactionActions investorTransactionActions, IPortfolioService investmentService, 
        ISeedwordsProvider provider, ITransactionService transactionService,
        IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<ClaimEndOfProjectRequest, Result<EndOfProjectTransactionDraft>>
    {
        public async Task<Result<EndOfProjectTransactionDraft>> Handle(ClaimEndOfProjectRequest request, CancellationToken cancellationToken)
        {
            var words = await provider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
                return Result.Failure<EndOfProjectTransactionDraft>(words.Error);
            
            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<EndOfProjectTransactionDraft>(accountBalanceResult.Error);
            
            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var investments = await investmentService.GetByWalletId(request.WalletId);
            if (investments.IsFailure)
                return Result.Failure<EndOfProjectTransactionDraft>(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<EndOfProjectTransactionDraft>("No investment found for this project");

            if (investment.InvestmentTransactionHex is null)
            {
                investment.InvestmentTransactionHex =  await transactionService.GetTransactionHexByIdAsync(investment.InvestmentTransactionHash);
                if (investment.InvestmentTransactionHex is null)
                    return Result.Failure<EndOfProjectTransactionDraft>("Could not find investment transaction in indexer: " + investment.InvestmentTransactionHash);
            }
            
            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<EndOfProjectTransactionDraft>(project.Error);
            
            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<EndOfProjectTransactionDraft>("Could not get a change address");
            
            var transactionInfo = await transactionService.GetTransactionInfoByIdAsync(investment.InvestmentTransactionHash);

            if (transactionInfo is null)
                return Result.Failure<EndOfProjectTransactionDraft>("Could not find transaction info");
            
            var stageIndex = transactionInfo.Outputs
                .Select((x, i) => i < 2 ? -1 // Skip the first two outputs (fee and op_return)
                    : string.IsNullOrEmpty(x.SpentInTransaction) ? i : -1)
                .First(x => x > 0);

            stageIndex -= 2; // Adjust for skipped outputs

            var endOfProjectTransaction = investorTransactionActions.RecoverEndOfProjectFunds(investment.InvestmentTransactionHex, project.Value.ToProjectInfo(), stageIndex,
                changeAddress, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()), new FeeEstimation(){FeeRate = request.SelectedFeeRate.SatsPerKilobyte});
            
           // var transactionId = await indexerService.PublishTransactionAsync(endOfProjectTransaction.Transaction.ToHex());

           return Result.Success(new EndOfProjectTransactionDraft()
               {
                   SignedTxHex = endOfProjectTransaction.Transaction.ToHex(),
                   TransactionFee = new Amount(Money.Satoshis(endOfProjectTransaction.TransactionFee)),
                   TransactionId = endOfProjectTransaction.Transaction.GetHash().ToString()
               });
        }
    }
}

using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Shared;

public static class PublishTransaction
{
    public record PublishTransactionRequest(string? WalletId, ProjectId? ProjectId, TransactionDraft TransactionDraft) : IRequest<Result<string>>;
    
    //TODO refresh the account info after publishing the transaction after the merge of penalty threshold is in
    public class Handler(IIndexerService indexerService, IPortfolioService portfolioService) : IRequestHandler<PublishTransactionRequest, Result<string>>
    {
        public async Task<Result<string>> Handle(PublishTransactionRequest request, CancellationToken cancellationToken)
        {
            //TODO add validations and perhaps the wallet id to make sure we are publishing from the correct wallet only
            
            if (string.IsNullOrEmpty(request.TransactionDraft.SignedTxHex))
            {
                return Result.Failure<string>("Transaction signature cannot be empty");
            }
            
            if (cancellationToken.IsCancellationRequested)
                return Result.Failure<string>("Operation was cancelled");

            var errorMessage = await indexerService.PublishTransactionAsync(request.TransactionDraft.SignedTxHex);
            
            if (!string.IsNullOrEmpty(errorMessage))
            {
                return Result.Failure<string>(errorMessage);
            }

            // Handle all transaction draft types if we have wallet/project context
            if (!string.IsNullOrEmpty(request.WalletId) && request.ProjectId != null)
            {
                var updateResult = await UpdateInvestmentRecordWithTransaction(
                    request.WalletId, 
                    request.ProjectId.Value, 
                    request.TransactionDraft);
                
                if (updateResult.IsFailure)
                    return Result.Failure<string>(updateResult.Error);
            }
            
            return Result.Success(request.TransactionDraft.TransactionId);
        }

        private async Task<Result> UpdateInvestmentRecordWithTransaction(
            string walletId, 
            string projectId, 
            TransactionDraft draft)
        {
            var investmentsResult = await portfolioService.GetByWalletId(walletId);
            if (investmentsResult.IsFailure)
                return Result.Failure(investmentsResult.Error);
            
            var investment = investmentsResult.Value?.ProjectIdentifiers
                .FirstOrDefault(i => i.ProjectIdentifier == projectId);
            
            // Handle each draft type
            switch (draft)
            {
                case InvestmentDraft investmentDraft:
                    if (investment != null)
                    {
                        // Update existing investment record
                        investment.InvestmentTransactionHash = draft.TransactionId;
                        investment.InvestmentTransactionHex = draft.SignedTxHex;
                        investment.InvestorPubKey = investmentDraft.InvestorKey;
                    }
                    else
                    {
                        // Create new investment record
                        investment = new InvestmentRecord
                        {
                            InvestmentTransactionHash = draft.TransactionId,
                            InvestmentTransactionHex = draft.SignedTxHex,
                            InvestorPubKey = investmentDraft.InvestorKey,
                            ProjectIdentifier = projectId,
                            UnfundedReleaseAddress = null, // No penalty path for direct investments
                            RequestEventId = null, // No founder approval request for investments below threshold
                            RequestEventTime = null
                        };
                    }
                    break;
                    
                case EndOfProjectTransactionDraft:
                    if (investment == null)
                        return Result.Failure("Investment record not found for end-of-project transaction");
                    
                    investment.EndOfProjectTransactionId = draft.TransactionId;
                    break;
                    
                case RecoveryTransactionDraft:
                    if (investment == null)
                        return Result.Failure("Investment record not found for recovery transaction");
                    
                    investment.RecoveryTransactionId = draft.TransactionId;
                    break;
                    
                case ReleaseTransactionDraft:
                    if (investment == null)
                        return Result.Failure("Investment record not found for release transaction");
                    
                    investment.RecoveryReleaseTransactionId = draft.TransactionId;
                    break;
                    
                default:
                    // Not a transaction type we track, just return success
                    return Result.Success();
            }
            
            // Save the investment record (common for all cases that modify the investment)
            await portfolioService.AddOrUpdate(walletId, investment);
            
            return Result.Success();
        }
    }
}
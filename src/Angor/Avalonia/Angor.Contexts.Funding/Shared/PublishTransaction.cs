using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Shared;

public static class PublishTransaction
{
    public record PublishTransactionRequest(Guid? WalletId, ProjectId? ProjectId, TransactionDraft TransactionDraft) : IRequest<Result<string>>;
    
    //TODO refresh the account info after publishing the transaction after the merge of penalty threshold is in
    public class Handler(IIndexerService indexerService, IPortfolioRepository portfolioRepository) : IRequestHandler<PublishTransactionRequest, Result<string>>
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

            // If this is an investment draft and we have wallet/project context, persist to portfolio
            if (request.TransactionDraft is InvestmentDraft investmentDraft && 
                request.WalletId.HasValue && 
                request.ProjectId != null)
            {
                var investmentRecord = new InvestmentRecord
                {
                    InvestmentTransactionHash = request.TransactionDraft.TransactionId,
                    InvestmentTransactionHex = request.TransactionDraft.SignedTxHex,
                    InvestorPubKey = investmentDraft.InvestorKey,
                    ProjectIdentifier = request.ProjectId.Value,
                    UnfundedReleaseAddress = null, // No penalty path for direct investments
                    RequestEventId = null, // No founder approval request for investments below threshold
                    RequestEventTime = null
                };

                var addResult = await portfolioRepository.Add(request.WalletId.Value, investmentRecord);
                
                // Don't fail the operation if portfolio storage fails since the transaction is already published
                // Just log/ignore the error
            }
            
            return Result.Success(request.TransactionDraft.TransactionId);
        }
    }
}
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class PublishAndStoreInvestorTransaction
{
    public record PublishAndStoreInvestorTransactionRequest(string? WalletId, Shared.ProjectId? ProjectId, Shared.TransactionDraft TransactionDraft) : IRequest<Result<string>>;

    //TODO refresh the account info after publishing the transaction after the merge of penalty threshold is in
    public class Handler(IIndexerService indexerService, IPortfolioService portfolioService) : IRequestHandler<PublishAndStoreInvestorTransactionRequest, Result<string>>
    {
        public async Task<Result<string>> Handle(PublishAndStoreInvestorTransactionRequest request, CancellationToken cancellationToken)
        {
            // Validate required parameters
            if (string.IsNullOrEmpty(request.WalletId))
                return Result.Failure<string>("WalletId is required for investor transactions");

            if (request.ProjectId is null)
                return Result.Failure<string>("ProjectId is required for investor transactions");

            if (string.IsNullOrEmpty(request.TransactionDraft.SignedTxHex))
                return Result.Failure<string>("Transaction signature cannot be empty");

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure<string>("Operation was cancelled");

            // Publish the transaction
            var errorMessage = await indexerService.PublishTransactionAsync(request.TransactionDraft.SignedTxHex);

            if (!string.IsNullOrEmpty(errorMessage))
                return Result.Failure<string>(errorMessage);

            // Update or create the investment record with the transaction ID
            var updateResult = await UpdateInvestmentRecordWithTransaction(
                request.WalletId,
                request.ProjectId.Value,
                request.TransactionDraft);

            if (updateResult.IsFailure)
                return Result.Failure<string>(updateResult.Error);

            return Result.Success(request.TransactionDraft.TransactionId);
        }

        private async Task<Result> UpdateInvestmentRecordWithTransaction(
            string walletId,
            string projectId,
            Shared.TransactionDraft draft)
        {
            var investmentsResult = await portfolioService.GetByWalletId(walletId);
            if (investmentsResult.IsFailure)
                return Result.Failure(investmentsResult.Error);

            var investment = investmentsResult.Value?.ProjectIdentifiers
                .FirstOrDefault(i => i.ProjectIdentifier == projectId);

            // Handle each draft type
            switch (draft) {
                case InvestmentDraft investmentDraft:
                    if (investment != null) {
                        // Update existing investment record
                        investment.InvestmentTransactionHash = draft.TransactionId;
                        investment.InvestmentTransactionHex = draft.SignedTxHex;
                        investment.InvestorPubKey = investmentDraft.InvestorKey;
                    }
                    else {
                        // Create new investment record
                        investment = new InvestmentRecord {
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

                    if (!string.IsNullOrEmpty(investment.EndOfProjectTransactionId))
                        return Result.Failure("End of project transaction has already been published");

                    investment.EndOfProjectTransactionId = draft.TransactionId;
                    break;

                case RecoveryTransactionDraft:
                    if (investment == null)
                        return Result.Failure("Investment record not found for recovery transaction");

                    if (!string.IsNullOrEmpty(investment.RecoveryTransactionId))
                        return Result.Failure("Recovery transaction has already been published");

                    investment.RecoveryTransactionId = draft.TransactionId;
                    break;

                case ReleaseTransactionDraft:
                    if (investment == null)
                        return Result.Failure("Investment record not found for release transaction");

                    if (string.IsNullOrEmpty(investment.RecoveryTransactionId))
                        return Result.Failure("Cannot release funds without a recovery transaction");

                    if (!string.IsNullOrEmpty(investment.RecoveryReleaseTransactionId))
                        return Result.Failure("Release transaction has already been published");

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

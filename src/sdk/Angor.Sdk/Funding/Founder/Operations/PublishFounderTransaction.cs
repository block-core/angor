using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class PublishFounderTransaction
{
    public record PublishFounderTransactionRequest(TransactionDraft TransactionDraft) : IRequest<Result<PublishFounderTransactionResponse>>;

    public record PublishFounderTransactionResponse(string TransactionId);

    public class Handler(IIndexerService indexerService, ILogger<Handler> logger) : IRequestHandler<PublishFounderTransactionRequest, Result<PublishFounderTransactionResponse>>
    {
        public async Task<Result<PublishFounderTransactionResponse>> Handle(PublishFounderTransactionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.TransactionDraft.SignedTxHex)) 
            {
                return Result.Failure<PublishFounderTransactionResponse>("Transaction signature cannot be empty");
            }

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure<PublishFounderTransactionResponse>("Operation was cancelled");

            const int maxAttempts = 3;
            var txId = request.TransactionDraft.TransactionId;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var errorMessage = await indexerService.PublishTransactionAsync(request.TransactionDraft.SignedTxHex);

                if (string.IsNullOrEmpty(errorMessage))
                {
                    logger.LogInformation("PublishFounderTransaction: broadcast succeeded on attempt {Attempt} for TxId={TxId}", attempt, txId);
                    return Result.Success(new PublishFounderTransactionResponse(txId));
                }

                // "Already in block chain" or "already known" means the tx exists — treat as success
                if (errorMessage.Contains("already in block", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("already known", StringComparison.OrdinalIgnoreCase) ||
                    errorMessage.Contains("txn-already-in-mempool", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("PublishFounderTransaction: tx {TxId} already exists (attempt {Attempt}): {Message}", txId, attempt, errorMessage);
                    return Result.Success(new PublishFounderTransactionResponse(txId));
                }

                logger.LogError("PublishFounderTransaction: broadcast attempt {Attempt}/{Max} failed for TxId={TxId}: {Message}",
                    attempt, maxAttempts, txId, errorMessage);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                }
            }

            return Result.Failure<PublishFounderTransactionResponse>($"Failed to publish founder transaction {txId} after {maxAttempts} attempts");
        }
    }
}

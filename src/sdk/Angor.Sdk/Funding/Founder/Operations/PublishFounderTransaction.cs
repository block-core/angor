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
                return Result.Failure<PublishFounderTransactionResponse>("Transaction signature cannot be empty");

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure<PublishFounderTransactionResponse>("Operation was cancelled");

            var txId = request.TransactionDraft.TransactionId;
            var publishResult = await TransactionBroadcastHelper.BroadcastWithRetryAsync(
                txId,
                () => indexerService.PublishTransactionAsync(request.TransactionDraft.SignedTxHex),
                (attempt, max, error) => logger.LogError(
                    "PublishFounderTransaction: broadcast attempt {Attempt}/{Max} failed for TxId={TxId}: {Message}",
                    attempt, max, txId, error),
                attempt => logger.LogInformation(
                    "PublishFounderTransaction: broadcast succeeded on attempt {Attempt} for TxId={TxId}",
                    attempt, txId),
                cancellationToken);

            return publishResult.Map(id => new PublishFounderTransactionResponse(id));
        }
    }
}

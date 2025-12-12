using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class PublishFounderTransaction
{
    public record PublishFounderTransactionRequest(TransactionDraft TransactionDraft) : IRequest<Result<string>>;

    public class Handler(IIndexerService indexerService) : IRequestHandler<PublishFounderTransactionRequest, Result<string>>
    {
        public async Task<Result<string>> Handle(PublishFounderTransactionRequest request, CancellationToken cancellationToken)
        {
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

            return Result.Success(request.TransactionDraft.TransactionId);
        }
    }
}

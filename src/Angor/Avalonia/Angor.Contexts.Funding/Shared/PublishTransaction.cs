using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Shared;

public static class PublishTransaction
{
    public record PublishTransactionRequest(TransactionDraft TransactionDraft) : IRequest<Result<string>>;
    
    //TODO refresh the account info after publishing the transaction after the merge of penalty threshold is in
    public class Handler(IIndexerService indexerService) : IRequestHandler<PublishTransactionRequest, Result<string>>
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
            
            return !string.IsNullOrEmpty(errorMessage) 
                ? Result.Failure<string>(errorMessage) 
                : Result.Success(request.TransactionDraft.TransactionId);
        }
    }
}
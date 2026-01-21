using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class PublishFounderTransaction
{
    public record PublishFounderTransactionRequest(TransactionDraft TransactionDraft, Project? Project = null) : IRequest<Result<PublishFounderTransactionResponse>>;

    public record PublishFounderTransactionResponse(string TransactionId);

    public class Handler(IIndexerService indexerService, IProjectService projectService) : IRequestHandler<PublishFounderTransactionRequest, Result<PublishFounderTransactionResponse>>
    {
        public async Task<Result<PublishFounderTransactionResponse>> Handle(PublishFounderTransactionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.TransactionDraft.SignedTxHex))
            {
                return Result.Failure<PublishFounderTransactionResponse>("Transaction signature cannot be empty");
            }

            if (cancellationToken.IsCancellationRequested)
                return Result.Failure<PublishFounderTransactionResponse>("Operation was cancelled");

            var errorMessage = await indexerService.PublishTransactionAsync(request.TransactionDraft.SignedTxHex);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                return Result.Failure<PublishFounderTransactionResponse>(errorMessage);
            }

            if (request.Project != null)
            {
                await projectService.AddAsync(request.Project);
            }

            return Result.Success(new PublishFounderTransactionResponse(request.TransactionDraft.TransactionId));
        }
    }
}

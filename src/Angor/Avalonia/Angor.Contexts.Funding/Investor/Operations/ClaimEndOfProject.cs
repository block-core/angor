using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class ClaimEndOfProject
{
    public record ClaimEndOfProjectRequest(Guid WalletId, ProjectId ProjectId, int StageIndex) : IRequest<Result>;

    // TODO: Placeholder handler
    public class ClaimEndOfProjectHandler : IRequestHandler<ClaimEndOfProjectRequest, Result>
    {
        public Task<Result> Handle(ClaimEndOfProjectRequest request, CancellationToken cancellationToken)
        {
            // TODO: Implement end of project claim transaction build/sign/publish
            return Task.FromResult(Result.Success());
        }
    }
}


using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class ReleaseFunds
{
    public record ReleaseFundsRequest(Guid WalletId, ProjectId ProjectId, int StageIndex) : IRequest<Result>;

    // TODO: Placeholder handler
    public class ReleaseFundsHandler : IRequestHandler<ReleaseFundsRequest, Result>
    {
        public Task<Result> Handle(ReleaseFundsRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success());
        }
    }
}


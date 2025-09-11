using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class RecoverFunds
{
    public record RecoverFundsRequest(Guid WalletId, ProjectId ProjectId, int StageIndex) : IRequest<Result>;

    // TODO: Placeholder handler
    public class RecoverFundsHandler : IRequestHandler<RecoverFundsRequest, Result>
    {
        public Task<Result> Handle(RecoverFundsRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Success());
        }
    }
}


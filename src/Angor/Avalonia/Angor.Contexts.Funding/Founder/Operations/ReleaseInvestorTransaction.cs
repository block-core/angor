using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class ReleaseInvestorTransaction
{
    public record ReleaseInvestorTransactionRequest(Guid WalletId, ProjectId ProjectId, IEnumerable<string> InvestorAddressList) : IRequest<Result>;

    public class ReleaseInvestorTransactionHandler : IRequestHandler<ReleaseInvestorTransactionRequest, Result>
    {
        public async Task<Result> Handle(ReleaseInvestorTransactionRequest request, CancellationToken cancellationToken)
        {
            // TODO: Implement the logic for Releasing an investor transaction.

            return Result.Success();
        }
    }
}
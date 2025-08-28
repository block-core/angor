using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public class SpendInvestorTransaction
{
    public record SpendInvestorTransactionRequest(ProjectId ProjectId, IEnumerable<SpendTransactionDto> ToSpend, long Feerate) : IRequest<Result>;

    public class SpendInvestorTransactionHandler : IRequestHandler<SpendInvestorTransactionRequest, Result>
    {
        public async Task<Result> Handle(SpendInvestorTransactionRequest request, CancellationToken cancellationToken)
        {
            // TODO: Implement the logic for spending an investor transaction.

            return Result.Success();
        }
    }
}
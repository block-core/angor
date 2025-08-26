using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public class GetClaimableTransactions
{
    public record GetClaimableTransactionsRequest(Guid WalletId, ProjectId ProjectId) : IRequest<Result<IEnumerable<ClaimableTransactionDto>>>;

    public class GetClaimableTransactionsHandler : IRequestHandler<GetClaimableTransactionsRequest, Result<IEnumerable<ClaimableTransactionDto>>>
    {
        public async Task<Result<IEnumerable<ClaimableTransactionDto>>> Handle(GetClaimableTransactionsRequest request, CancellationToken cancellationToken)
        {
            // TODO: Implement the logic for retrieving claimable transactions for a project.
            // Mocked data
            IEnumerable<ClaimableTransactionDto> list = new List<ClaimableTransactionDto>()
            {
                new()
                {
                    StageId = 1,
                    Amount = new Amount(1234),
                    InvestorAddress = "1234abcd5678efgh9012ijklmnopqrstuvwx",
                    ClaimStatus = ClaimStatus.Pending
                },
                new()
                {
                    StageId = 1,
                    Amount = new Amount(1234),
                    InvestorAddress = "1234abcd5678efgh9012ijklmnopqrstuvwx",
                    ClaimStatus = ClaimStatus.SpentByFounder
                },
                new()
                {
                    StageId = 2,
                    Amount = new Amount(1234),
                    InvestorAddress = "1234abcd5678efgh9012ijklmnopqrstuvwx",
                    ClaimStatus = ClaimStatus.Unspent
                }
            };
            return Result.Success(list);
        }
    }
}
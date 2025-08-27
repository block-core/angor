using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetReleaseableTransactions
{
    public record GetReleaseableTransactionsRequest(Guid WalletId, ProjectId ProjectId) : IRequest<Result<IEnumerable<ReleaseableTransactionDto>>>;

    public class GetClaimableTransactionsHandler : IRequestHandler<GetReleaseableTransactionsRequest, Result<IEnumerable<ReleaseableTransactionDto>>>
    {
        public async Task<Result<IEnumerable<ReleaseableTransactionDto>>> Handle(GetReleaseableTransactionsRequest request, CancellationToken cancellationToken)
        {
            // TODO: Implement the logic for retrieving claimable transactions for a project.
            // Mocked data
            IEnumerable<ReleaseableTransactionDto> list = new List<ReleaseableTransactionDto>()
            {
                new()
                {
                    InvestorAddress = "1234abcd5678efgh9012ijklmnopqrstu22",
                    Arrived = DateTime.Now.AddDays(-10),
                    Released = null,
                    Approved = DateTime.Now.AddDays(-5),
                },
                new()
                {
                    InvestorAddress = "1234abcd5678efgh9012ijklmnfasdfpqrstuvwx",
                    Arrived = DateTime.Now.AddDays(-90),
                    Released = null,
                    Approved = DateTime.Now.AddDays(-89),
                },
                new()
                {
                    InvestorAddress = "123gg34cd5678efgh9012ijklmnopqrstuvwx",
                    Arrived = DateTime.Now.AddDays(-50),
                    Released = null,
                    Approved = DateTime.Now.AddDays(-15),
                }
            };
            return Result.Success(list);
        }
    }
}
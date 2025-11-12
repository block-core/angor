using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetClaimableTransactions
{
    public record GetClaimableTransactionsRequest(string WalletId, ProjectId ProjectId) : IRequest<Result<IEnumerable<ClaimableTransactionDto>>>;

    public class GetClaimableTransactionsHandler(IProjectInvestmentsService projectInvestmentsService) : IRequestHandler<GetClaimableTransactionsRequest, Result<IEnumerable<ClaimableTransactionDto>>>
    {
        public async Task<Result<IEnumerable<ClaimableTransactionDto>>> Handle(GetClaimableTransactionsRequest request, CancellationToken cancellationToken)
        {
            var resultList = await projectInvestmentsService.ScanFullInvestments(request.ProjectId.Value);

            if (resultList.IsFailure)
            {
                return Result.Failure<IEnumerable<ClaimableTransactionDto>>(resultList.Error);
            }
            
            var list = resultList.Value.SelectMany(x => x.Items
                .Select<StageDataTrx, ClaimableTransactionDto>(item => 
                new ClaimableTransactionDto()
                {
                    StageId = x.StageIndex,
                    Amount = new Amount(item.Amount),
                    InvestorAddress = item.InvestorPublicKey,
                    ClaimStatus = item.SpentType switch
                    {
                        "founder" => ClaimStatus.SpentByFounder, 
                        "investor" => ClaimStatus.WithdrawByInvestor,
                        "pending" => ClaimStatus.Pending,
                        _ => ClaimStatus.Unspent
                    },
                }));
            
            return Result.Success(list);
        }
    }
}
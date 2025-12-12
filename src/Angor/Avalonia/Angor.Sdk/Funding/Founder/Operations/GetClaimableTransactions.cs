using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Infrastructure.Interfaces;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class GetClaimableTransactions
{
    public record GetClaimableTransactionsRequest(WalletId WalletId, ProjectId ProjectId) : IRequest<Result<GetClaimableTransactionsResponse>>;

    public record GetClaimableTransactionsResponse(IEnumerable<ClaimableTransactionDto> Transactions);

    public class GetClaimableTransactionsHandler(IProjectInvestmentsService projectInvestmentsService) : IRequestHandler<GetClaimableTransactionsRequest, Result<GetClaimableTransactionsResponse>>
    {
        public async Task<Result<GetClaimableTransactionsResponse>> Handle(GetClaimableTransactionsRequest request, CancellationToken cancellationToken)
        {
            var resultList = await projectInvestmentsService.ScanFullInvestments(request.ProjectId.Value);

            if (resultList.IsFailure)
            {
                return Result.Failure<GetClaimableTransactionsResponse>(resultList.Error);
            }

            var list = resultList.Value.SelectMany(stageData => stageData.Items
                    .Select<StageDataTrx, ClaimableTransactionDto>(item =>
                       new ClaimableTransactionDto()
                       {
                           StageId = stageData.StageIndex,
                           StageNumber = stageData.StageIndex + 1,
                           Amount = new Amount(item.Amount),
                           DynamicReleaseDate = stageData.IsDynamic ? stageData.StageDate : null,
                           InvestorAddress = item.InvestorPublicKey,
                           ClaimStatus = DetermineClaimStatus(item, stageData),
                       }));

            return Result.Success(new GetClaimableTransactionsResponse(list));
        }

        private static ClaimStatus DetermineClaimStatus(StageDataTrx item, StageData stageData)
        {
            // Check if stage release date hasn't been reached yet (works for both dynamic and fixed stages)
            if (stageData.StageDate > DateTime.UtcNow)
            {
                return ClaimStatus.Locked;
            }

            // If not spent, return Unspent
            if (!item.IsSpent)
            {
                return ClaimStatus.Unspent;
            }

            // Map ProjectScriptType to ClaimStatus for spent transactions
            if (item.ProjectScriptType?.ScriptType != null)
            {
                return item.ProjectScriptType.ScriptType switch
                {
                    ProjectScriptTypeEnum.Founder => ClaimStatus.SpentByFounder,
                    ProjectScriptTypeEnum.InvestorWithPenalty => ClaimStatus.WithdrawByInvestor,
                    ProjectScriptTypeEnum.InvestorNoPenalty => ClaimStatus.WithdrawByInvestor,
                    ProjectScriptTypeEnum.EndOfProject => ClaimStatus.WithdrawByInvestor,
                    ProjectScriptTypeEnum.Unknown => ClaimStatus.Pending,
                    _ => ClaimStatus.Invalid
                };
            }

            // Fallback to SpentType if ProjectScriptType is not available (backward compatibility)
            return item.SpentType switch
            {
                "founder" => ClaimStatus.SpentByFounder,
                "investor" => ClaimStatus.WithdrawByInvestor,
                "pending" => ClaimStatus.Pending,
                _ => ClaimStatus.Unspent
            };
        }
    }
}
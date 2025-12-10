using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class GetClaimableTransactions
{
    public record GetClaimableTransactionsRequest(WalletId WalletId, ProjectId ProjectId) : IRequest<Result<IEnumerable<ClaimableTransactionDto>>>;

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
                           StageNumber = x.StageIndex + 1,
                           Amount = new Amount(item.Amount),
                           DynamicReleaseDate = item.DynamicReleaseDate,
                           InvestorAddress = item.InvestorPublicKey,
                           ClaimStatus = DetermineClaimStatus(item),
                       }));

            return Result.Success(list);
        }

        private static ClaimStatus DetermineClaimStatus(StageDataTrx item)
        {
            // Check if stage has a dynamic release date that hasn't been reached yet
            if (item.DynamicReleaseDate.HasValue && item.DynamicReleaseDate.Value > DateTime.UtcNow)
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
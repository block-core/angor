using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

/// <summary>
/// Computes per-investor share breakdown for a project.
/// Each investor's total investment, share percentage of the total pool,
/// and the amount already claimed (spent) by the founder are calculated
/// from on-chain stage data.
///
/// For "Fund" type projects, the share is labelled "as of now" because
/// new funds can always be added, changing the total pool.
/// For "Invest" type projects, the share also includes how much of the
/// investor's share has been claimed by the founder.
/// </summary>
public static class GetInvestorShares
{
    public record GetInvestorSharesRequest(ProjectId ProjectId) : IRequest<Result<GetInvestorSharesResponse>>;

    public record GetInvestorSharesResponse(
        long TotalInvested,
        int TotalInvestors,
        IReadOnlyList<InvestorShareDto> Investors);

    public class GetInvestorSharesHandler(IProjectInvestmentsService projectInvestmentsService)
        : IRequestHandler<GetInvestorSharesRequest, Result<GetInvestorSharesResponse>>
    {
        public async Task<Result<GetInvestorSharesResponse>> Handle(
            GetInvestorSharesRequest request, CancellationToken cancellationToken)
        {
            var stagesResult = await projectInvestmentsService.ScanFullInvestments(request.ProjectId.Value);

            if (stagesResult.IsFailure)
            {
                return Result.Failure<GetInvestorSharesResponse>(stagesResult.Error);
            }

            var stages = stagesResult.Value.ToList();

            if (!stages.Any())
            {
                return Result.Success(new GetInvestorSharesResponse(0, 0, Array.Empty<InvestorShareDto>()));
            }

            // Flatten all stage transaction items and group by investor public key
            var allItems = stages.SelectMany(s => s.Items)
                .Where(i => !string.IsNullOrEmpty(i.InvestorPublicKey))
                .ToList();

            var totalInvested = allItems.Sum(i => i.Amount);
            var totalSpentByFounder = allItems.Where(i => i.IsSpent && IsFounderSpend(i.SpentType)).Sum(i => i.Amount);

            var investorGroups = allItems
                .GroupBy(i => i.InvestorPublicKey)
                .Select(g =>
                {
                    var investorTotal = g.Sum(i => i.Amount);
                    var investorSpent = g.Where(i => i.IsSpent && IsFounderSpend(i.SpentType)).Sum(i => i.Amount);
                    var sharePercentage = totalInvested > 0
                        ? Math.Round((double)investorTotal / totalInvested * 100, 2)
                        : 0;
                    var spentSharePercentage = investorTotal > 0
                        ? Math.Round((double)investorSpent / investorTotal * 100, 2)
                        : 0;

                    return new InvestorShareDto(
                        InvestorPublicKey: g.Key,
                        InvestorNpub: g.First().InvestorNpub ?? "",
                        TotalInvested: investorTotal,
                        SharePercentage: sharePercentage,
                        AmountClaimedByFounder: investorSpent,
                        ClaimedPercentage: spentSharePercentage);
                })
                .OrderByDescending(i => i.TotalInvested)
                .ToList();

            return Result.Success(new GetInvestorSharesResponse(
                totalInvested,
                investorGroups.Count,
                investorGroups));
        }

        /// <summary>
        /// Determines if a spend type represents a founder claim/withdrawal
        /// (as opposed to investor recovery or penalty).
        /// </summary>
        private static bool IsFounderSpend(string spentType)
        {
            if (string.IsNullOrEmpty(spentType))
                return false;

            // Founder spends are typically stage releases / withdrawals.
            // Investor spends are recoveries, penalties, unfunded releases.
            // The SpentType values come from the indexer and match ProjectScriptType names.
            return spentType.Contains("Founder", StringComparison.OrdinalIgnoreCase)
                   || spentType.Equals("AngorKey", StringComparison.OrdinalIgnoreCase);
        }
    }
}

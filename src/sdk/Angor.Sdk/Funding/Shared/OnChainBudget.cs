using Angor.Sdk.Common;
using Angor.Shared.Protocol;

namespace Angor.Sdk.Funding.Shared;

/// <summary>
/// Single source of truth for how many sats a deploy (project creation) payment must deliver to the funding
/// address, so the on-chain invoice, the Lightning swap and the address monitor never use different budgets.
/// </summary>
public static class OnChainBudget
{
    /// <summary>Create fee + miner fee for the base tx + change headroom above dust.</summary>
    public static long DeployRequiredSats(long feeRateSatsPerVb) =>
        NetworkConfiguration.AngorCreateFeeSats
        + InvestmentFeeEstimator.EstimateInvestmentTxFee(0, feeRateSatsPerVb)
        + ProtocolConstants.DustThresholdSats;

    /// <summary>Smallest amount that still lets the deploy tx be built (no change headroom).</summary>
    public static long DeployMinimumAcceptableSats(long feeRateSatsPerVb) =>
        NetworkConfiguration.AngorCreateFeeSats
        + InvestmentFeeEstimator.EstimateInvestmentTxFee(0, feeRateSatsPerVb);
}
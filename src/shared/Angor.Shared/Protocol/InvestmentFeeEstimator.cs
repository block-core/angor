namespace Angor.Shared.Protocol;

/// <summary>
/// Single source of truth for estimating the total on-chain amount a user must
/// deliver to a funding address so that an investment transaction can be built
/// and signed exclusively from UTXOs on that address.
/// Used by both the web app (Blazor) and the desktop app (via the SDK) so the
/// invoice amount and the transaction-build fee budget can never drift apart.
/// </summary>
public static class InvestmentFeeEstimator
{
    /// <summary>
    /// Base investment tx size with a single P2WPKH input:
    ///   ~10.5 vB tx overhead
    ///   ~68   vB 1 P2WPKH input
    ///    43   vB 1 P2WSH output (angor fee)
    ///   ~99   vB 1 OP_RETURN output
    ///    31   vB 1 P2WPKH change output
    /// </summary>
    public const int BaseTxVbytes = 252;

    /// <summary>Each stage adds one P2TR output (~43 vB).</summary>
    public const int PerStageVbytes = 43;

    /// <summary>Each additional P2WPKH input adds ~68 vB.</summary>
    public const int PerInputVbytes = 68;

    /// <summary>
    /// Headroom for the Boltz claim transaction fee. The claim tx that sweeps the
    /// swap lockup into the funding address pays its own miner fee (built locally
    /// at ~2 sat/vB, ~111 vB), which is deducted from the amount that lands
    /// on-chain. Boltz's advertised swap fees do NOT include it.
    /// </summary>
    public const long LightningClaimFeeHeadroomSats = 300;

    /// <summary>Estimated virtual size of the investment transaction in vbytes.</summary>
    public static int EstimateInvestmentTxVbytes(int stageCount, int inputCount = 1)
    {
        if (stageCount < 0) stageCount = 0;
        if (inputCount < 1) inputCount = 1;
        return BaseTxVbytes + (stageCount * PerStageVbytes) + ((inputCount - 1) * PerInputVbytes);
    }

    /// <summary>Estimated miner fee of the investment transaction in satoshis.</summary>
    public static long EstimateInvestmentTxFee(int stageCount, long feeRateSatsPerVbyte, int inputCount = 1)
    {
        return feeRateSatsPerVbyte * EstimateInvestmentTxVbytes(stageCount, inputCount);
    }

    /// <summary>
    /// Total on-chain amount required at the funding address:
    /// investment amount + Angor fee + estimated investment tx miner fee.
    /// </summary>
    public static long EstimateOnChainRequired(
        long investmentAmountSats,
        int angorFeePercentage,
        int stageCount,
        long feeRateSatsPerVbyte,
        int inputCount = 1)
    {
        long angorFee = (investmentAmountSats * angorFeePercentage) / 100;
        return investmentAmountSats + angorFee + EstimateInvestmentTxFee(stageCount, feeRateSatsPerVbyte, inputCount);
    }

    /// <summary>
    /// Total amount to request when funds arrive via a Lightning (Boltz reverse swap)
    /// claim: same as <see cref="EstimateOnChainRequired"/> plus headroom for the
    /// locally-built claim transaction fee that is deducted before funds land.
    /// </summary>
    public static long EstimateLightningRequired(
        long investmentAmountSats,
        int angorFeePercentage,
        int stageCount,
        long feeRateSatsPerVbyte)
    {
        return EstimateOnChainRequired(investmentAmountSats, angorFeePercentage, stageCount, feeRateSatsPerVbyte)
               + LightningClaimFeeHeadroomSats;
    }
}

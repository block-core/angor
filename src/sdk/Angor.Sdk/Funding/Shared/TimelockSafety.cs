namespace Angor.Sdk.Funding.Shared;

/// <summary>
/// Safety margin applied when gating CLTV/CSV timelocked spends on wall-clock time.
///
/// Bitcoin nodes evaluate time-based locktimes against the chain tip's median-time-past
/// (MTP, the median of the last 11 block timestamps), which typically lags wall-clock
/// time by 30-90 minutes (worst case about 2 hours). Comparing release dates against
/// DateTime.UtcNow alone makes outputs look spendable before the network will actually
/// accept the transaction — sendrawtransaction rejects it with "non-final" (-26).
/// </summary>
public static class TimelockSafety
{
    /// <summary>
    /// How long after a timelock's release date we keep treating the output as locked,
    /// so that MTP has caught up by the time the user is offered the spend.
    /// </summary>
    public static readonly TimeSpan MedianTimePastBuffer = TimeSpan.FromHours(2);
}

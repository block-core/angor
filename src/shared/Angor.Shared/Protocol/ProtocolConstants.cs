namespace Angor.Shared.Protocol;

/// <summary>
/// Protocol-level constants enforced across all Angor transaction building and validation.
/// These are consensus-critical values — changing them affects how projects are created
/// and how transactions are built. All existing projects created before these constants
/// were enforced may not satisfy these checks.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>
    /// Minimum fee rate in satoshis per kilobyte (1000 sat/kB = 1 sat/vB).
    /// This is the Bitcoin network minimum relay fee. Any fee rate below this
    /// will be rejected to prevent fee-rate sniping where a founder sets
    /// dust-level fees to make investor recovery transactions uneconomical.
    /// </summary>
    public const long MinFeeRateSatsPerKb = 1000;

    /// <summary>
    /// Dust threshold in satoshis for Taproot (P2TR) outputs.
    /// Outputs below this value are considered "dust" and will be rejected
    /// by Bitcoin Core nodes. Stage outputs must exceed this threshold.
    /// Calculated as: 3 * (8 + 1 + 1 + 34) = 330 bytes * minRelayFee.
    /// </summary>
    public const long DustThresholdSats = 330;

    /// <summary>
    /// Minimum penalty period in days for Invest and Fund project types.
    /// A penalty period shorter than this provides insufficient protection
    /// against investors immediately recovering funds after investing.
    /// </summary>
    public const int MinPenaltyDays = 10;

    /// <summary>
    /// Maximum penalty period in days. Constrained by BIP-68 CSV encoding:
    /// maximum relative timelock is 65535 * 512 seconds = ~388 days.
    /// We cap at 365 days to stay safely within the protocol limit.
    /// </summary>
    public const int MaxPenaltyDays = 365;

    /// <summary>
    /// Minimum number of days between consecutive stage release dates.
    /// Prevents stages from being set too close together, which could
    /// allow a founder to drain all funds in rapid succession.
    /// </summary>
    public const int MinDaysBetweenStages = 1;

    /// <summary>
    /// Minimum number of days the first stage must be in the future
    /// relative to the project's start date. This ensures investors
    /// have time to invest before the first stage is released.
    /// </summary>
    public const int MinDaysUntilFirstStage = 1;

    /// <summary>
    /// Minimum target amount in satoshis (0.001 BTC = 100,000 sats).
    /// Projects below this threshold are likely not economically viable
    /// given transaction fee overhead.
    /// </summary>
    public const long MinTargetAmountSats = 100_000;
}

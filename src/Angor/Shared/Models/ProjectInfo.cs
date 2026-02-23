namespace Angor.Shared.Models;

/// <summary>
/// Defines the type of project and how it handles funding and stages.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Investment project: Fixed stages with start/end dates and target amount.
    /// Investors commit funds during a defined period and stages are predetermined.
    /// Penalty applies for early withdrawal.
    /// </summary>
    Invest = 0,

    /// <summary>
    /// Fund project: Dynamic stages with no fixed dates.
    /// No specific investment window, stages can be added/modified dynamically.
    /// May not have a target amount. Penalty applies for early withdrawal.
    /// </summary>
    Fund = 1,

    /// <summary>
    /// Subscribe project: Similar to Fund but without penalties.
    /// Dynamic stages, no fixed dates, and investors can withdraw without penalty.
    /// Suitable for ongoing support or subscription-based funding.
    /// </summary>
    Subscribe = 2
}

/// <summary>
/// Frequency at which dynamic stages occur.
/// Used for Fund and Subscribe project types.
/// </summary>
public enum StageFrequency
{
    /// <summary>
    /// Stages released weekly.
    /// </summary>
    Weekly = 0,

    /// <summary>
    /// Stages released every two weeks (bi-weekly).
    /// </summary>
    Biweekly = 1,

    /// <summary>
    /// Stages released monthly.
    /// </summary>
    Monthly = 2,

    /// <summary>
    /// Stages released every two months (bi-monthly).
    /// </summary>
    BiMonthly = 3,

    /// <summary>
    /// Stages released quarterly (every 3 months).
    /// </summary>
    Quarterly = 4
}

/// <summary>
/// Defines how payout days are calculated for dynamic stage patterns.
/// </summary>
public enum PayoutDayType
{
    /// <summary>
    /// Payout is calculated from the investment start date by adding fixed intervals.
    /// Example: If start date is Feb 15, and frequency is Monthly, stages are Mar 15, Apr 15, May 15, etc.
    /// </summary>
    FromStartDate = 0,
    
    /// <summary>
    /// Payout occurs on a specific day of the month (1-31).
    /// Only applicable for Monthly, BiMonthly, and Quarterly frequencies.
    /// Example: PayoutDay = 1 means payout on the 1st of each month.
    /// </summary>
    SpecificDayOfMonth = 1,
    
    /// <summary>
    /// Payout occurs on a specific day of the week (0=Sunday, 1=Monday, ..., 6=Saturday).
    /// Only applicable for Weekly and Biweekly frequencies.
    /// Example: PayoutDay = 1 means payout every Monday.
    /// </summary>
    SpecificDayOfWeek = 2
}

/// <summary>
/// Encapsulates the public information related to an investment project.
/// This data, when combined with additional keys owned by an investor, facilitates the creation of an investment transaction.
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Schema version of the ProjectInfo structure.
    /// Used for backward compatibility and data migration.
    /// Version 1: Original schema (pre-ProjectType support)
    /// Version 2: Added ProjectType enum and dynamic stages support
    /// Default is 2 for new projects, but can be 1 for legacy projects.
    /// </summary>
    public int Version { get; set; } = 2;

    /// <summary>
    /// The type of project (Invest, Fund, or Subscribe) which determines how stages and funding work.
    /// Default is Invest for backward compatibility.
    /// Only applicable for Version 2+. Version 1 projects are always treated as Invest type.
    /// </summary>
    public ProjectType ProjectType { get; set; } = ProjectType.Invest;

    /// <summary>
    /// The founder's public key used for project identification and transactions.
    /// </summary>
    public string FounderKey { get; set; } = string.Empty;

    /// <summary>
    /// Recovery key for the founder in case the primary key is compromised.
    /// </summary>
    public string FounderRecoveryKey { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this project.
    /// </summary>
    public string ProjectIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Nostr public key associated with this project for decentralized communication.
    /// </summary>
    public string NostrPubKey { get; set; } = string.Empty;

    /// <summary>
    /// The blockchain network this project operates on (e.g., "Bitcoin", "BitcoinTestnet", "BitcoinRegtest", "angornet", "liquid").
    /// Used to ensure investors connect to the correct network and prevent cross-network transaction errors.
    /// </summary>
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the funding period.
    /// Required for ProjectType.Invest, optional for Fund and Subscribe types.
    /// Defaults to DateTime.MinValue for Fund/Subscribe types where it's not used.
    /// </summary>
    public DateTime StartDate { get; set; } = DateTime.MinValue;

    /// <summary>
    /// End date of the funding period.
    /// Required for ProjectType.Invest, ignored for Fund and Subscribe types.
    /// Defaults to DateTime.MinValue for Fund/Subscribe types where it's not used.
    /// </summary>
    public DateTime EndDate { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Number of days funds are locked if an investor withdraws early.
    /// Applies to Invest and Fund types. Ignored for Subscribe type (no penalty).
    /// </summary>
    public int PenaltyDays { get; set; }

    /// <summary>
    /// Emergency date when all remaining funds can be released.
    /// Provides a safety mechanism for all project types.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Target funding amount in satoshis.
    /// Required for ProjectType.Invest, optional for Fund and Subscribe types.
    /// </summary>
    public long TargetAmount { get; set; }

    /// <summary>
    /// Optional threshold amount for penalty calculation.
    /// Can be used across all project types to define when penalties apply.
    /// </summary>
    public long? PenaltyThreshold { get; set; }

    /// <summary>
    /// List of funding stages defining the release schedule.
    /// For Invest: Fixed stages with predetermined dates and percentages.
    /// For Fund/Subscribe: Dynamic stages that can be added or modified over time.
    /// </summary>
    public List<Stage> Stages { get; set; } = new();

    /// <summary>
    /// Configuration for project seeders (early supporters).
    /// </summary>
    public ProjectSeeders ProjectSeeders { get; set; } = new();

    /// <summary>
    /// Dynamic stage patterns available for this project.
    /// Only applicable for Fund and Subscribe project types.
    /// Invest projects don't use this - they have fixed stages defined in Stages list.
    /// Allows founders to define multiple subscription/funding options (e.g., 3-month, 6-month, 12-month).
    /// </summary>
    public List<DynamicStagePattern> DynamicStagePatterns { get; set; } = new();

    /// <summary>
    /// Indicates whether stages can be dynamically added or modified after project creation.
    /// True for Fund and Subscribe types, false for Invest type.
    /// </summary>
    public bool AllowDynamicStages => ProjectType == ProjectType.Fund || ProjectType == ProjectType.Subscribe;

    /// <summary>
    /// Indicates whether the project requires a defined investment window (start and end dates).
    /// True for Invest type, false for Fund and Subscribe types.
    /// </summary>
    public bool RequiresInvestmentWindow => ProjectType == ProjectType.Invest;

    /// <summary>
    /// Indicates whether penalties apply for early withdrawal.
    /// True for Invest and Fund types, false for Subscribe type.
    /// </summary>
    public bool HasPenalty => ProjectType == ProjectType.Invest || ProjectType == ProjectType.Fund;

    /// <summary>
    /// Indicates whether a target amount is required.
    /// True for Invest type, false for Fund and Subscribe types.
    /// </summary>
    public bool RequiresTargetAmount => ProjectType == ProjectType.Invest;
}
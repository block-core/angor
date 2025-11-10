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
    public string FounderKey { get; set; }
    
    /// <summary>
    /// Recovery key for the founder in case the primary key is compromised.
    /// </summary>
    public string FounderRecoveryKey { get; set; }
    
    /// <summary>
    /// Unique identifier for this project.
    /// </summary>
    public string ProjectIdentifier { get; set; }
    
    /// <summary>
    /// Nostr public key associated with this project for decentralized communication.
    /// </summary>
    public string NostrPubKey { get; set; }
    
  /// <summary>
    /// Start date of the funding period.
    /// Required for ProjectType.Invest, optional for Fund and Subscribe types.
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
  /// End date of the funding period.
    /// Required for ProjectType.Invest, ignored for Fund and Subscribe types.
    /// </summary>
    public DateTime EndDate { get; set; }
    
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
  /// Indicates whether this is a legacy Version 1 project.
  /// Version 1 projects don't have ProjectType support and are treated as Invest type.
    /// </summary>
    public bool IsLegacyVersion => Version < 2;
    
    /// <summary>
    /// Gets the effective project type, accounting for legacy versions.
    /// Version 1 projects are always treated as Invest type regardless of ProjectType property.
    /// </summary>
  public ProjectType EffectiveProjectType => IsLegacyVersion ? ProjectType.Invest : ProjectType;
    
    /// <summary>
    /// Indicates whether stages can be dynamically added or modified after project creation.
    /// True for Fund and Subscribe types, false for Invest type.
    /// Always false for legacy Version 1 projects.
    /// </summary>
    public bool AllowDynamicStages => !IsLegacyVersion && (EffectiveProjectType == ProjectType.Fund || EffectiveProjectType == ProjectType.Subscribe);
    
    /// <summary>
    /// Indicates whether the project requires a defined investment window (start and end dates).
    /// True for Invest type, false for Fund and Subscribe types.
  /// Always true for legacy Version 1 projects.
    /// </summary>
    public bool RequiresInvestmentWindow => IsLegacyVersion || EffectiveProjectType == ProjectType.Invest;
    
    /// <summary>
    /// Indicates whether penalties apply for early withdrawal.
    /// True for Invest and Fund types, false for Subscribe type.
 /// Always true for legacy Version 1 projects.
/// </summary>
  public bool HasPenalty => IsLegacyVersion || EffectiveProjectType == ProjectType.Invest || EffectiveProjectType == ProjectType.Fund;
  
    /// <summary>
    /// Indicates whether a target amount is required.
    /// True for Invest type, false for Fund and Subscribe types.
    /// Always true for legacy Version 1 projects.
 /// </summary>
    public bool RequiresTargetAmount => IsLegacyVersion || EffectiveProjectType == ProjectType.Invest;
}
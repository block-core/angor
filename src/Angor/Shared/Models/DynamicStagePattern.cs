namespace Angor.Shared.Models;

/// <summary>
/// Defines a pattern for dynamically generated stages.
/// Used for Fund and Subscribe project types to offer flexible subscription/funding options.
/// </summary>
public class DynamicStagePattern
{
    /// <summary>
    /// Unique numeric identifier for this pattern (0-255).
    /// Examples: 0 = "3-month monthly", 1 = "6-month monthly", 2 = "12-week biweekly", etc.
  /// This ID is stored in the OP_RETURN script alongside the investment data.
    /// </summary>
    public byte PatternId { get; set; }

    /// <summary>
    /// Display name for this subscription/funding pattern.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
  /// Description of this pattern.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// How often stages are released (weekly, monthly, quarterly, etc.).
    /// </summary>
    public StageFrequency Frequency { get; set; }

  /// <summary>
    /// Number of stages in this pattern.
/// Example: 6 for a 6-month subscription, 12 for a yearly subscription with monthly stages.
    /// The total investment amount will be split equally across all stages.
    /// </summary>
    public int StageCount { get; set; }

    /// <summary>
    /// Defines how payout days are calculated.
    /// </summary>
    public PayoutDayType PayoutDayType { get; set; } = PayoutDayType.FromStartDate;

    /// <summary>
    /// Specific payout day value (interpretation depends on PayoutDayType):
    /// - FromStartDate: Not used (ignored)
    /// - SpecificDayOfMonth: Day of month (1-31)
    /// - SpecificDayOfWeek: Day of week (0=Sunday, 1=Monday, ..., 6=Saturday)
    /// </summary>
    public int PayoutDay { get; set; }
}

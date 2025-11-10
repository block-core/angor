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
/// Helper class for working with dynamic stage patterns and epoch-based dates.
/// </summary>
public static class DynamicStageHelper
{
    /// <summary>
    /// Reference epoch date for compact date storage: January 1, 2025 UTC.
    /// This allows storing dates as days since epoch using only 2 bytes (uint16).
    /// Supports dates from Jan 1, 2025 to ~Dec 31, 2204 (179 years).
    /// </summary>
    public static readonly DateTime EpochDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Converts a DateTime to days since epoch (Jan 1, 2025).
    /// </summary>
    /// <param name="date">The date to convert (must be >= EpochDate)</param>
    /// <returns>Number of days since epoch (0-65535)</returns>
    /// <exception cref="ArgumentOutOfRangeException">If date is before epoch or exceeds uint16 range</exception>
    public static ushort ToDaysSinceEpoch(DateTime date)
    {
        if (date < EpochDate)
            throw new ArgumentOutOfRangeException(nameof(date), $"Date must be >= {EpochDate:yyyy-MM-dd}");

        var days = (date.Date - EpochDate.Date).TotalDays;

        if (days > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(date), $"Date exceeds maximum supported range (~179 years from epoch)");

        return (ushort)days;
    }

    /// <summary>
    /// Converts days since epoch back to a DateTime.
    /// </summary>
    /// <param name="daysSinceEpoch">Number of days since Jan 1, 2025</param>
    /// <returns>DateTime in UTC</returns>
    public static DateTime FromDaysSinceEpoch(ushort daysSinceEpoch)
    {
        return EpochDate.AddDays(daysSinceEpoch);
    }

    /// <summary>
    /// Gets the TimeSpan duration for a given stage frequency.
    /// </summary>
    /// <param name="frequency">The stage frequency</param>
    /// <returns>TimeSpan representing the duration</returns>
    public static TimeSpan GetFrequencyDuration(StageFrequency frequency)
    {
        return frequency switch
        {
            StageFrequency.Weekly => TimeSpan.FromDays(7),
            StageFrequency.Biweekly => TimeSpan.FromDays(14),
            StageFrequency.Monthly => TimeSpan.FromDays(30),
            StageFrequency.BiMonthly => TimeSpan.FromDays(60),
            StageFrequency.Quarterly => TimeSpan.FromDays(90),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), $"Unknown frequency: {frequency}")
        };
    }

    /// <summary>
    /// Computes stage release dates from a pattern and investment start date.
    /// </summary>
    /// <param name="pattern">The dynamic stage pattern</param>
    /// <param name="investmentStartDate">When the investment begins</param>
    /// <returns>List of stage release dates</returns>
    public static List<Stage> ComputeStagesFromPattern(DynamicStagePattern pattern, DateTime investmentStartDate)
    {
        var stages = new List<Stage>();
        var percentagePerStage = 100m / pattern.StageCount;

        for (int i = 0; i < pattern.StageCount; i++)
        {
            DateTime releaseDate;

            if (pattern.PayoutDayType == PayoutDayType.FromStartDate)
            {
                // Simple: add fixed intervals from start date
                var duration = GetFrequencyDuration(pattern.Frequency);
                releaseDate = investmentStartDate.Add(duration * i);
            }
            else if (pattern.PayoutDayType == PayoutDayType.SpecificDayOfMonth)
            {
                // Payout on specific day of month (e.g., 1st, 15th)
                releaseDate = CalculateNextMonthlyPayoutDate(investmentStartDate, pattern.Frequency, pattern.PayoutDay, i);
            }
            else // SpecificDayOfWeek
            {
                // Payout on specific day of week (e.g., Monday)
                releaseDate = CalculateNextWeeklyPayoutDate(investmentStartDate, pattern.Frequency, pattern.PayoutDay, i);
            }

            stages.Add(new Stage
            {
                ReleaseDate = releaseDate,
                AmountToRelease = percentagePerStage
            });
        }

        return stages;
    }

    private static DateTime CalculateNextMonthlyPayoutDate(DateTime startDate, StageFrequency frequency, int dayOfMonth, int stageIndex)
    {
        // Determine how many months to add based on frequency
        int monthsToAdd = frequency switch
        {
            StageFrequency.Monthly => stageIndex,
            StageFrequency.BiMonthly => stageIndex * 2,
            StageFrequency.Quarterly => stageIndex * 3,
            _ => throw new ArgumentException($"Frequency {frequency} does not support SpecificDayOfMonth")
        };

        // Start from the first occurrence of the target day
        var targetDate = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Adjust to the target day of month (handle months with fewer days)
        var daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
        var actualDay = Math.Min(dayOfMonth, daysInMonth);
        targetDate = new DateTime(targetDate.Year, targetDate.Month, actualDay, 0, 0, 0, DateTimeKind.Utc);

        // If the target date is before the start date, move to next month
        if (targetDate < startDate)
        {
            targetDate = targetDate.AddMonths(1);
            daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
            actualDay = Math.Min(dayOfMonth, daysInMonth);
            targetDate = new DateTime(targetDate.Year, targetDate.Month, actualDay, 0, 0, 0, DateTimeKind.Utc);
        }

        // Add the appropriate number of months for this stage
        targetDate = targetDate.AddMonths(monthsToAdd);

        // Adjust for months with fewer days (e.g., Feb 31 -> Feb 28/29)
        daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
        actualDay = Math.Min(dayOfMonth, daysInMonth);

        return new DateTime(targetDate.Year, targetDate.Month, actualDay, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime CalculateNextWeeklyPayoutDate(DateTime startDate, StageFrequency frequency, int dayOfWeek, int stageIndex)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "DayOfWeek must be 0-6 (Sunday-Saturday)");

        // Determine how many weeks to add based on frequency
        int weeksToAdd = frequency switch
        {
            StageFrequency.Weekly => stageIndex,
            StageFrequency.Biweekly => stageIndex * 2,
            _ => throw new ArgumentException($"Frequency {frequency} does not support SpecificDayOfWeek")
        };

        // Find the first occurrence of the target day of week on or after start date
        var targetDayOfWeek = (DayOfWeek)dayOfWeek;
        var currentDate = startDate.Date;

        while (currentDate.DayOfWeek != targetDayOfWeek)
        {
            currentDate = currentDate.AddDays(1);
        }

        // Add the appropriate number of weeks for this stage
        return currentDate.AddDays(weeksToAdd * 7);
    }
}

/// <summary>
/// Defines a pattern for dynamically generated stages.
/// Used for Fund and Subscribe project types to offer flexible subscription/funding options.
/// </summary>
public class DynamicStagePattern
{
    /// <summary>
    /// Unique identifier for this pattern (e.g., "6-month-sub", "3-month-sub").
    /// </summary>
    public string PatternId { get; set; }

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
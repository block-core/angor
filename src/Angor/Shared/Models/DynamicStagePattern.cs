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
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of this pattern.
    /// </summary>
    public string Description { get; set; } = string.Empty;

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

    /// <summary>
    /// The fixed investment amount in satoshis for this pattern.
    /// This is MANDATORY for Subscribe project types where the founder defines the subscription price.
    /// For Fund projects, this can be null/0 to allow investors to choose their own amount.
    /// When set, the total investment will be this amount, split equally across all stages.
    /// </summary>
    public long? Amount { get; set; }

    /// <summary>
    /// Indicates whether this pattern has a fixed amount (typically used for Subscribe projects).
    /// When true, the Amount property must be set and investors cannot choose their own amount.
    /// </summary>
    public bool HasFixedAmount => Amount.HasValue && Amount.Value > 0;

    /// <summary>
    /// Gets a collection of standard predefined patterns for Fund and Subscribe projects.
    /// Includes various monthly, biweekly, weekly, and quarterly options.
    /// </summary>
    /// <returns>List of predefined DynamicStagePattern options</returns>
    public static List<DynamicStagePattern> GetStandardPatterns()
    {
        return new List<DynamicStagePattern>
        {
            // Monthly patterns (1st of month)
            new DynamicStagePattern
            {
                PatternId     = 0,
                Name          = "3-Month Monthly",
                Description   = "3 monthly payments on the 1st of each month",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 3,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 1,
                Name          = "6-Month Monthly",
                Description   = "6 monthly payments on the 1st of each month",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 6,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 2,
                Name          = "9-Month Monthly",
                Description   = "9 monthly payments on the 1st of each month",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 9,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 3,
                Name          = "12-Month Monthly",
                Description   = "12 monthly payments on the 1st of each month (Annual)",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 12,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 4,
                Name          = "24-Month Monthly",
                Description   = "24 monthly payments on the 1st of each month (2 Years)",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 24,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },

            // Monthly patterns (15th of month)
            new DynamicStagePattern
            {
                PatternId     = 5,
                Name          = "3-Month Monthly (Mid-month)",
                Description   = "3 monthly payments on the 15th of each month",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 3,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 15
            },
            new DynamicStagePattern
            {
                PatternId     = 6,
                Name          = "6-Month Monthly (Mid-month)",
                Description   = "6 monthly payments on the 15th of each month",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 6,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 15
            },
            new DynamicStagePattern
            {
                PatternId     = 7,
                Name          = "12-Month Monthly (Mid-month)",
                Description   = "12 monthly payments on the 15th of each month",
                Frequency     = StageFrequency.Monthly,
                StageCount    = 12,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 15
            },

            // Biweekly patterns (every 2 weeks from start date)
            new DynamicStagePattern
            {
                PatternId     = 8,
                Name          = "6-Period Biweekly",
                Description   = "6 payments every 2 weeks (~3 months)",
                Frequency     = StageFrequency.Biweekly,
                StageCount    = 6,
                PayoutDayType = PayoutDayType.FromStartDate,
                PayoutDay     = 0
            },
            new DynamicStagePattern
            {
                PatternId     = 9,
                Name          = "12-Period Biweekly",
                Description   = "12 payments every 2 weeks (~6 months)",
                Frequency     = StageFrequency.Biweekly,
                StageCount    = 12,
                PayoutDayType = PayoutDayType.FromStartDate,
                PayoutDay     = 0
            },
            new DynamicStagePattern
            {
                PatternId     = 10,
                Name          = "26-Period Biweekly",
                Description   = "26 payments every 2 weeks (1 year)",
                Frequency     = StageFrequency.Biweekly,
                StageCount    = 26,
                PayoutDayType = PayoutDayType.FromStartDate,
                PayoutDay     = 0
            },

            // Weekly patterns (specific day of week - Monday)
            new DynamicStagePattern
            {
                PatternId     = 11,
                Name          = "12-Week Weekly",
                Description   = "12 weekly payments every Monday (~3 months)",
                Frequency     = StageFrequency.Weekly,
                StageCount    = 12,
                PayoutDayType = PayoutDayType.SpecificDayOfWeek,
                PayoutDay     = 1 // Monday
            },
            new DynamicStagePattern
            {
                PatternId     = 12,
                Name          = "26-Week Weekly",
                Description   = "26 weekly payments every Monday (~6 months)",
                Frequency     = StageFrequency.Weekly,
                StageCount    = 26,
                PayoutDayType = PayoutDayType.SpecificDayOfWeek,
                PayoutDay     = 1 // Monday
            },
            new DynamicStagePattern
            {
                PatternId     = 13,
                Name          = "52-Week Weekly",
                Description   = "52 weekly payments every Monday (1 year)",
                Frequency     = StageFrequency.Weekly,
                StageCount    = 52,
                PayoutDayType = PayoutDayType.SpecificDayOfWeek,
                PayoutDay     = 1 // Monday
            },

            // Quarterly patterns
            new DynamicStagePattern
            {
                PatternId     = 14,
                Name          = "4-Quarter Quarterly",
                Description   = "4 quarterly payments (1 year)",
                Frequency     = StageFrequency.Quarterly,
                StageCount    = 4,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 15,
                Name          = "8-Quarter Quarterly",
                Description   = "8 quarterly payments (2 years)",
                Frequency     = StageFrequency.Quarterly,
                StageCount    = 8,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },

            // Bi-monthly patterns (every 2 months)
            new DynamicStagePattern
            {
                PatternId     = 16,
                Name          = "3-Period Bi-monthly",
                Description   = "3 payments every 2 months (~6 months)",
                Frequency     = StageFrequency.BiMonthly,
                StageCount    = 3,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 17,
                Name          = "6-Period Bi-monthly",
                Description   = "6 payments every 2 months (1 year)",
                Frequency     = StageFrequency.BiMonthly,
                StageCount    = 6,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            },
            new DynamicStagePattern
            {
                PatternId     = 18,
                Name          = "12-Period Bi-monthly",
                Description   = "12 payments every 2 months (2 years)",
                Frequency     = StageFrequency.BiMonthly,
                StageCount    = 12,
                PayoutDayType = PayoutDayType.SpecificDayOfMonth,
                PayoutDay     = 1
            }
        };
    }

    /// <summary>
    /// Gets standard monthly patterns (1st of month) with common durations.
    /// </summary>
    /// <returns>List of monthly patterns</returns>
    public static List<DynamicStagePattern> GetMonthlyPatterns()
    {
        return GetStandardPatterns()
            .Where(p => p.Frequency == StageFrequency.Monthly && p.PayoutDay == 1)
            .ToList();
    }

    /// <summary>
    /// Gets standard weekly patterns (every Monday).
    /// </summary>
    /// <returns>List of weekly patterns</returns>
    public static List<DynamicStagePattern> GetWeeklyPatterns()
    {
        return GetStandardPatterns()
            .Where(p => p.Frequency == StageFrequency.Weekly)
            .ToList();
    }

    /// <summary>
    /// Gets standard biweekly patterns.
    /// </summary>
    /// <returns>List of biweekly patterns</returns>
    public static List<DynamicStagePattern> GetBiweeklyPatterns()
    {
        return GetStandardPatterns()
            .Where(p => p.Frequency == StageFrequency.Biweekly)
            .ToList();
    }

    /// <summary>
    /// Gets standard quarterly patterns.
    /// </summary>
    /// <returns>List of quarterly patterns</returns>
    public static List<DynamicStagePattern> GetQuarterlyPatterns()
    {
        return GetStandardPatterns()
            .Where(p => p.Frequency == StageFrequency.Quarterly)
            .ToList();
    }

    /// <summary>
    /// Gets a specific pattern by its PatternId.
    /// </summary>
    /// <param name="patternId">The pattern ID to look up</param>
    /// <returns>The matching pattern, or null if not found</returns>
    public static DynamicStagePattern? GetPatternById(byte patternId)
    {
        return GetStandardPatterns().FirstOrDefault(p => p.PatternId == patternId);
    }
}

using Angor.Shared.Models;

namespace Angor.Shared.Utilities;

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
            var releaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(investmentStartDate, pattern, i);

            stages.Add(new Stage
            {
                ReleaseDate = releaseDate,
                AmountToRelease = percentagePerStage
            });
        }

        return stages;
    }
}

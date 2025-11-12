namespace Angor.Shared.Models;

/// <summary>
/// Provides methods for calculating dynamic stage release dates based on various patterns.
/// </summary>
public static class DynamicStageCalculator
{
    /// <summary>
    /// Calculates the release date for a dynamic stage based on the pattern and investment start date.
    /// </summary>
    public static DateTime CalculateDynamicStageReleaseDate(DateTime investmentStartDate, DynamicStagePattern pattern, int stageIndex)
    {
        if (pattern.PayoutDayType == PayoutDayType.FromStartDate)
        {
            // Simple: add fixed intervals from start date
            var duration = DynamicStageHelper.GetFrequencyDuration(pattern.Frequency);
            return investmentStartDate.Add(duration * (stageIndex + 1)); // +1 because first stage is after first interval
        }
        else if (pattern.PayoutDayType == PayoutDayType.SpecificDayOfMonth)
        {
            // Payout on specific day of month (e.g., 1st, 15th)
            return CalculateMonthlyPayoutDate(investmentStartDate, pattern.Frequency, pattern.PayoutDay, stageIndex);
        }
        else // SpecificDayOfWeek
        {
            // Payout on specific day of week (e.g., Monday)
            return CalculateWeeklyPayoutDate(investmentStartDate, pattern.Frequency, pattern.PayoutDay, stageIndex);
        }
    }

    /// <summary>
    /// Calculates the payout date for a stage that occurs on a specific day of the month.
    /// Handles edge cases like requesting the 31st in months with fewer days.
    /// </summary>
    public static DateTime CalculateMonthlyPayoutDate(DateTime startDate, StageFrequency frequency, int dayOfMonth, int stageIndex)
    {
        // Determine how many months to add based on frequency
        int monthsToAdd = frequency switch
        {
            StageFrequency.Monthly => stageIndex,
            StageFrequency.BiMonthly => stageIndex * 2,
            StageFrequency.Quarterly => stageIndex * 3,
            _ => throw new ArgumentException($"Frequency {frequency} does not support SpecificDayOfMonth", nameof(frequency))
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

    /// <summary>
    /// Calculates the payout date for a stage that occurs on a specific day of the week.
    /// </summary>
    public static DateTime CalculateWeeklyPayoutDate(DateTime startDate, StageFrequency frequency, int dayOfWeek, int stageIndex)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "DayOfWeek must be 0-6 (Sunday-Saturday)");

        // Determine how many weeks to add based on frequency
        int weeksToAdd = frequency switch
        {
            StageFrequency.Weekly => stageIndex,
            StageFrequency.Biweekly => stageIndex * 2,
            _ => throw new ArgumentException($"Frequency {frequency} does not support SpecificDayOfWeek", nameof(frequency))
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

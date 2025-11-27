using Angor.Shared.Models;
using Angor.Shared.Utilities;

namespace Angor.Test.Models;

public class DynamicStageCalculatorTests
{
    #region CalculateDynamicStageReleaseDate Tests

    [Fact]
    public void CalculateDynamicStageReleaseDate_FromStartDate_Monthly_ReturnsCorrectDates()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.FromStartDate,
            Frequency = StageFrequency.Monthly,
            StageCount = 3
        };

        // Act & Assert
        var stage0 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 0);
        var stage1 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 1);
        var stage2 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 2);

        Assert.Equal(new DateTime(2025, 2, 14, 0, 0, 0, DateTimeKind.Utc), stage0); // +30 days
        Assert.Equal(new DateTime(2025, 3, 16, 0, 0, 0, DateTimeKind.Utc), stage1); // +60 days
        Assert.Equal(new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc), stage2); // +90 days
    }

    [Fact]
    public void CalculateDynamicStageReleaseDate_FromStartDate_Weekly_ReturnsCorrectDates()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.FromStartDate,
            Frequency = StageFrequency.Weekly,
            StageCount = 4
        };

        // Act & Assert
        var stage0 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 0);
        var stage1 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 1);
        var stage2 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 2);
        var stage3 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 3);

        Assert.Equal(new DateTime(2025, 1, 22, 0, 0, 0, DateTimeKind.Utc), stage0); // +7 days
        Assert.Equal(new DateTime(2025, 1, 29, 0, 0, 0, DateTimeKind.Utc), stage1); // +14 days
        Assert.Equal(new DateTime(2025, 2, 5, 0, 0, 0, DateTimeKind.Utc), stage2);  // +21 days
        Assert.Equal(new DateTime(2025, 2, 12, 0, 0, 0, DateTimeKind.Utc), stage3); // +28 days
    }

    [Fact]
    public void CalculateDynamicStageReleaseDate_SpecificDayOfMonth_ReturnsCorrectDates()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Monthly,
            PayoutDay = 15, // 15th of each month
            StageCount = 3
        };

        // Act
        var stage0 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 0);
        var stage1 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 1);
        var stage2 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 2);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), stage0); // Jan 15
        Assert.Equal(new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc), stage1); // Feb 15
        Assert.Equal(new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), stage2); // Mar 15
    }

    [Fact]
    public void CalculateDynamicStageReleaseDate_SpecificDayOfWeek_ReturnsCorrectDates()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc); // Wednesday
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfWeek,
            Frequency = StageFrequency.Weekly,
            PayoutDay = 1, // Monday
            StageCount = 3
        };

        // Act
        var stage0 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 0);
        var stage1 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 1);
        var stage2 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 2);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc), stage0); // Next Monday
        Assert.Equal(new DateTime(2025, 1, 27, 0, 0, 0, DateTimeKind.Utc), stage1); // Following Monday
        Assert.Equal(new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc), stage2);  // Next Monday
    }

    #endregion

    #region CalculateMonthlyPayoutDate Tests

    [Fact]
    public void CalculateMonthlyPayoutDate_Monthly_FirstOccurrenceAfterStartDate()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 15, 0);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_Monthly_MovesToNextMonthIfDayPassed()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc); // After the 15th

        // Act
        var result = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 15, 0);

        // Assert
        Assert.Equal(new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc), result); // Next month
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_Monthly_HandlesFebruary()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act - Request 31st in February
        var result = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 31, 1);

        // Assert - Should be Feb 28 (2025 is not a leap year)
        Assert.Equal(new DateTime(2025, 2, 28, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_Monthly_HandlesLeapYear()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act - Request 31st in February of leap year
        var result = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 31, 1);

        // Assert - Should be Feb 29 (2024 is a leap year)
        Assert.Equal(new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_BiMonthly_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var stage0 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.BiMonthly, 15, 0);
        var stage1 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.BiMonthly, 15, 1);
        var stage2 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.BiMonthly, 15, 2);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), stage0); // Jan 15
        Assert.Equal(new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc), stage1); // Mar 15 (2 months later)
        Assert.Equal(new DateTime(2025, 5, 15, 0, 0, 0, DateTimeKind.Utc), stage2); // May 15 (4 months later)
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_Quarterly_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var stage0 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Quarterly, 1, 0);
        var stage1 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Quarterly, 1, 1);
        var stage2 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Quarterly, 1, 2);

        // Assert
        Assert.Equal(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), stage0);  // Feb 1 (next month with day 1)
        Assert.Equal(new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc), stage1);  // May 1 (3 months later)
        Assert.Equal(new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc), stage2);  // Aug 1 (6 months later)
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_InvalidFrequency_ThrowsException()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
       DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Weekly, 15, 0));
    }

    [Fact]
    public void CalculateMonthlyPayoutDate_HandlesMonthsWith30Days()
    {
        // Arrange
        var startDate = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act - Request 31st in April (has 30 days)
        var result = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 31, 1);

        // Assert - Should be April 30
        Assert.Equal(new DateTime(2025, 4, 30, 0, 0, 0, DateTimeKind.Utc), result);
    }

    #endregion

    #region CalculateWeeklyPayoutDate Tests

    [Fact]
    public void CalculateWeeklyPayoutDate_Weekly_FindsNextOccurrence()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc); // Wednesday

        // Act - Find next Monday (day 1)
        var result = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 0);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc), result); // Next Monday
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_Weekly_MultipleStages()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc); // Wednesday

        // Act - Every Monday
        var stage0 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 0);
        var stage1 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 1);
        var stage2 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 2);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc), stage0); // Jan 20 (Monday)
        Assert.Equal(new DateTime(2025, 1, 27, 0, 0, 0, DateTimeKind.Utc), stage1); // Jan 27 (Monday)
        Assert.Equal(new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc), stage2);  // Feb 3 (Monday)
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_Biweekly_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc); // Wednesday

        // Act - Every other Friday (day 5)
        var stage0 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Biweekly, 5, 0);
        var stage1 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Biweekly, 5, 1);
        var stage2 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Biweekly, 5, 2);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 17, 0, 0, 0, DateTimeKind.Utc), stage0); // Jan 17 (Friday)
        Assert.Equal(new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc), stage1); // Jan 31 (2 weeks later)
        Assert.Equal(new DateTime(2025, 2, 14, 0, 0, 0, DateTimeKind.Utc), stage2); // Feb 14 (4 weeks later)
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_SameDay_UsesSameDay()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc); // Monday

        // Act - Monday (day 1) - same day as start
        var result = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 0);

        // Assert - Should use the same Monday
        Assert.Equal(new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_Sunday_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc); // Monday

        // Act - Next Sunday (day 0)
        var result = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 0, 0);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 26, 0, 0, 0, DateTimeKind.Utc), result); // Next Sunday
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_Saturday_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc); // Monday

        // Act - Next Saturday (day 6)
        var result = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 6, 0);

        // Assert
        Assert.Equal(new DateTime(2025, 1, 25, 0, 0, 0, DateTimeKind.Utc), result); // Next Saturday
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(100)]
    public void CalculateWeeklyPayoutDate_InvalidDayOfWeek_ThrowsException(int invalidDay)
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
              DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, invalidDay, 0));
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_InvalidFrequency_ThrowsException()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
              DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Monthly, 1, 0));
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void CalculateMonthlyPayoutDate_YearBoundary_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 11, 10, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var stage0 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 15, 0);
        var stage1 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 15, 1);
        var stage2 = DynamicStageCalculator.CalculateMonthlyPayoutDate(startDate, StageFrequency.Monthly, 15, 2);

        // Assert
        Assert.Equal(new DateTime(2024, 11, 15, 0, 0, 0, DateTimeKind.Utc), stage0); // Nov 15
        Assert.Equal(new DateTime(2024, 12, 15, 0, 0, 0, DateTimeKind.Utc), stage1); // Dec 15
        Assert.Equal(new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc), stage2);  // Jan 15 (next year)
    }

    [Fact]
    public void CalculateWeeklyPayoutDate_YearBoundary_CalculatesCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2024, 12, 25, 0, 0, 0, DateTimeKind.Utc); // Wednesday

        // Act - Every Monday
        var stage0 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 0);
        var stage1 = DynamicStageCalculator.CalculateWeeklyPayoutDate(startDate, StageFrequency.Weekly, 1, 1);

        // Assert
        Assert.Equal(new DateTime(2024, 12, 30, 0, 0, 0, DateTimeKind.Utc), stage0); // Dec 30 (Monday)
        Assert.Equal(new DateTime(2025, 1, 6, 0, 0, 0, DateTimeKind.Utc), stage1);   // Jan 6 (next year)
    }

    [Fact]
    public void CalculateDynamicStageReleaseDate_AllFrequencies_ProduceDistinctDates()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var frequencies = new[]
          {
            StageFrequency.Weekly,
            StageFrequency.Biweekly,
            StageFrequency.Monthly,
            StageFrequency.BiMonthly,
            StageFrequency.Quarterly
        };

        foreach (var frequency in frequencies)
        {
            var pattern = new DynamicStagePattern
            {
                PayoutDayType = PayoutDayType.FromStartDate,
                Frequency = frequency,
                StageCount = 3
            };

            // Act
            var stage0 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 0);
            var stage1 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 1);
            var stage2 = DynamicStageCalculator.CalculateDynamicStageReleaseDate(startDate, pattern, 2);

            // Assert - Each stage should be later than the previous
            Assert.True(stage0 > startDate, $"{frequency}: Stage 0 should be after start date");
            Assert.True(stage1 > stage0, $"{frequency}: Stage 1 should be after stage 0");
            Assert.True(stage2 > stage1, $"{frequency}: Stage 2 should be after stage 1");
        }
    }

    #endregion
}

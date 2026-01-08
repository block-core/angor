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

    #region DynamicStagePattern Amount Validation Tests

    [Fact]
    public void DynamicStagePattern_HasFixedAmount_ReturnsTrueWhenAmountIsSet()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = 100000 // 100,000 sats
        };

        // Assert
        Assert.True(pattern.HasFixedAmount);
    }

    [Fact]
    public void DynamicStagePattern_HasFixedAmount_ReturnsFalseWhenAmountIsNull()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = null
        };

        // Assert
        Assert.False(pattern.HasFixedAmount);
    }

    [Fact]
    public void DynamicStagePattern_HasFixedAmount_ReturnsFalseWhenAmountIsZero()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = 0
        };

        // Assert
        Assert.False(pattern.HasFixedAmount);
    }

    [Fact]
    public void FundingParameters_ValidateSubscribe_ThrowsWhenNoAmount()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = null,
            PayoutDayType = PayoutDayType.FromStartDate
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Subscribe,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 100000,
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => fundingParameters.Validate(projectInfo));
        Assert.Contains("fixed Amount", exception.Message);
    }

    [Fact]
    public void FundingParameters_ValidateSubscribe_SucceedsWithMatchingAmount()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = 100000,
            PayoutDayType = PayoutDayType.FromStartDate
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Subscribe,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 100000, // Matches pattern amount
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act - should not throw
        fundingParameters.Validate(projectInfo);
    }

    [Fact]
    public void FundingParameters_ValidateSubscribe_ThrowsWhenAmountDoesNotMatch()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = 100000,
            PayoutDayType = PayoutDayType.FromStartDate
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Subscribe,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 50000, // Does not match pattern amount
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => fundingParameters.Validate(projectInfo));
        Assert.Contains("fixed amount", exception.Message);
    }

    [Fact]
    public void FundingParameters_ValidateFund_SucceedsWithoutAmount()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = null,
            PayoutDayType = PayoutDayType.FromStartDate
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Fund,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 100000,
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act - should not throw (Fund doesn't require fixed amount)
        fundingParameters.Validate(projectInfo);
    }

    [Fact]
    public void FundingParameters_Validate_ThrowsWhenStageCountIsZero()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 0,
            Amount = 100000,
            PayoutDayType = PayoutDayType.FromStartDate
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Subscribe,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 100000,
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => fundingParameters.Validate(projectInfo));
        Assert.Contains("StageCount", exception.Message);
    }

    [Fact]
    public void FundingParameters_Validate_ThrowsForInvalidPayoutDayOfMonth()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = 100000,
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            PayoutDay = 32 // Invalid
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Subscribe,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 100000,
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => fundingParameters.Validate(projectInfo));
        Assert.Contains("PayoutDay", exception.Message);
    }

    [Fact]
    public void FundingParameters_Validate_ThrowsForInvalidPayoutDayOfWeek()
    {
        // Arrange
        var pattern = new DynamicStagePattern
        {
            StageCount = 3,
            Amount = 100000,
            PayoutDayType = PayoutDayType.SpecificDayOfWeek,
            PayoutDay = 7 // Invalid (must be 0-6)
        };
        var projectInfo = new ProjectInfo
        {
            ProjectType = ProjectType.Subscribe,
            DynamicStagePatterns = new List<DynamicStagePattern> { pattern }
        };
        var fundingParameters = new FundingParameters
        {
            InvestorKey = "testkey",
            TotalInvestmentAmount = 100000,
            InvestmentStartDate = DateTime.UtcNow,
            PatternIndex = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => fundingParameters.Validate(projectInfo));
        Assert.Contains("PayoutDay", exception.Message);
    }

    #endregion
}

using Angor.Shared.Models;

namespace Angor.Test.Models;

public class DynamicStagePatternTests
{
    #region GenerateDescription / DisplayDescription Tests

    [Fact]
    public void GenerateDescription_SpecificDayOfMonth_Monthly_1st_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Monthly,
            StageCount = 3,
            PayoutDay = 1
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("3 monthly payments on the 1st of each month", result);
    }

    [Fact]
    public void GenerateDescription_SpecificDayOfMonth_Monthly_26th_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Monthly,
            StageCount = 6,
            PayoutDay = 26
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("6 monthly payments on the 26th of each month", result);
    }

    [Fact]
    public void GenerateDescription_SpecificDayOfMonth_Monthly_15th_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Monthly,
            StageCount = 12,
            PayoutDay = 15
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("12 monthly payments on the 15th of each month", result);
    }

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(22, "22nd")]
    [InlineData(23, "23rd")]
    [InlineData(26, "26th")]
    [InlineData(29, "29th")]
    public void GenerateDescription_OrdinalSuffixes_AreCorrect(int day, string expectedOrdinal)
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Monthly,
            StageCount = 3,
            PayoutDay = day
        };

        var result = pattern.GenerateDescription();

        Assert.Contains(expectedOrdinal, result);
    }

    [Fact]
    public void GenerateDescription_SpecificDayOfWeek_Weekly_Monday_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfWeek,
            Frequency = StageFrequency.Weekly,
            StageCount = 12,
            PayoutDay = (int)DayOfWeek.Monday
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("12 weekly payments every Monday", result);
    }

    [Fact]
    public void GenerateDescription_SpecificDayOfWeek_Biweekly_Wednesday_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfWeek,
            Frequency = StageFrequency.Biweekly,
            StageCount = 6,
            PayoutDay = (int)DayOfWeek.Wednesday
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("6 biweekly payments every Wednesday", result);
    }

    [Fact]
    public void GenerateDescription_FromStartDate_Monthly_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.FromStartDate,
            Frequency = StageFrequency.Monthly,
            StageCount = 6
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("6 payments monthly from start date", result);
    }

    [Fact]
    public void GenerateDescription_FromStartDate_Biweekly_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.FromStartDate,
            Frequency = StageFrequency.Biweekly,
            StageCount = 26
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("26 payments every 2 weeks from start date", result);
    }

    [Fact]
    public void DisplayDescription_ReflectsActualPayoutDay_WhenDescriptionIsStale()
    {
        // Simulate a pattern stored with a stale hardcoded description
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Monthly,
            StageCount = 3,
            PayoutDay = 26,
            Description = "3 monthly payments on the 1st of each month" // stale/incorrect
        };

        // DisplayDescription should reflect the actual PayoutDay (26), not the stale Description
        Assert.Equal("3 monthly payments on the 26th of each month", pattern.DisplayDescription);
    }

    [Fact]
    public void GenerateDescription_Quarterly_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.Quarterly,
            StageCount = 4,
            PayoutDay = 1
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("4 quarterly payments on the 1st of the payment month", result);
    }

    [Fact]
    public void GenerateDescription_BiMonthly_ReturnsCorrectText()
    {
        var pattern = new DynamicStagePattern
        {
            PayoutDayType = PayoutDayType.SpecificDayOfMonth,
            Frequency = StageFrequency.BiMonthly,
            StageCount = 6,
            PayoutDay = 15
        };

        var result = pattern.GenerateDescription();

        Assert.Equal("6 bi-monthly payments on the 15th of the payment month", result);
    }

    #endregion
}

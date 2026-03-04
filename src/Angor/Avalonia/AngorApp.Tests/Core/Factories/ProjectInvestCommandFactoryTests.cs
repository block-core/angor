using AngorApp.Core.Factories;
using Angor.Shared.Models;
using FluentAssertions;

namespace AngorApp.Tests.Core.Factories;

public class ProjectInvestCommandFactoryTests
{
    [Fact]
    public void IsInsideInvestmentPeriod_returns_false_when_funding_has_not_started()
    {
        var now = DateTimeOffset.UtcNow;
        var fundingStart = now.AddMinutes(1);
        var fundingEnd = now.AddHours(1);

        var result = ProjectInvestCommandFactory.IsInsideInvestmentPeriod(now, fundingStart, fundingEnd);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsInsideInvestmentPeriod_returns_true_when_inside_funding_window()
    {
        var now = DateTimeOffset.UtcNow;
        var fundingStart = now.AddMinutes(-1);
        var fundingEnd = now.AddMinutes(1);

        var result = ProjectInvestCommandFactory.IsInsideInvestmentPeriod(now, fundingStart, fundingEnd);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanInvest_returns_true_for_fund_when_started_even_if_end_date_is_in_the_past()
    {
        var now = DateTimeOffset.UtcNow;
        var fundingStart = now.AddMinutes(-1);
        var fundingEnd = now.AddDays(-10);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Fund, now, fundingStart, fundingEnd);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanInvest_returns_false_for_fund_when_not_started()
    {
        var now = DateTimeOffset.UtcNow;
        var fundingStart = now.AddMinutes(1);
        var fundingEnd = now.AddDays(-10);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Fund, now, fundingStart, fundingEnd);

        result.Should().BeFalse();
    }
}

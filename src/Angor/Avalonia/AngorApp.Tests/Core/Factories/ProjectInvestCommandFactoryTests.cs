using AngorApp.Core.Factories;
using Angor.Shared.Models;
using FluentAssertions;

namespace AngorApp.Tests.Core.Factories;

public class ProjectInvestCommandFactoryTests
{
    [Fact]
    public void IsInsideInvestmentPeriod_returns_false_when_funding_has_not_started()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddMinutes(1);
        var fundingEnd = now.AddHours(1);

        var result = ProjectInvestCommandFactory.IsInsideInvestmentPeriod(now, fundingStart, fundingEnd);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsInsideInvestmentPeriod_returns_true_when_inside_funding_window()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddMinutes(-1);
        var fundingEnd = now.AddMinutes(1);

        var result = ProjectInvestCommandFactory.IsInsideInvestmentPeriod(now, fundingStart, fundingEnd);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanInvest_returns_true_for_fund_when_started_even_if_end_date_is_in_the_past()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddMinutes(-1);
        var fundingEnd = now.AddDays(-10);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Fund, now, fundingStart, fundingEnd);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanInvest_returns_false_for_fund_when_not_started()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddMinutes(1);
        var fundingEnd = now.AddDays(-10);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Fund, now, fundingStart, fundingEnd);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanInvest_returns_false_for_invest_when_funding_ended_and_debug_off()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddDays(-30);
        var fundingEnd = now.AddDays(-10);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Invest, now, fundingStart, fundingEnd, isDebugMode: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanInvest_returns_true_for_invest_when_funding_ended_and_debug_on()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddDays(-30);
        var fundingEnd = now.AddDays(-10);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Invest, now, fundingStart, fundingEnd, isDebugMode: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanInvest_returns_true_for_invest_when_not_started_with_debug_on()
    {
        var now = DateTime.UtcNow;
        var fundingStart = now.AddDays(1);
        var fundingEnd = now.AddDays(30);

        var result = ProjectInvestCommandFactory.CanInvest(ProjectType.Invest, now, fundingStart, fundingEnd, isDebugMode: true);

        result.Should().BeTrue();
    }
}

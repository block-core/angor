using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Tests.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Tests for GetPenaltiesHandler.RefreshPenalties, which derives the two fields the penalty
/// UI renders: IsExpired (gates the release action) and DaysLeftForPenalty (the countdown).
///
/// Regression context: these were computed independently — DaysLeftForPenalty truncated to
/// calendar days and ignored the median-time-past safety buffer, while IsExpired used instant
/// precision and applied it. Between midnight on the expiry day and expiry + buffer the pair
/// disagreed, and the UI rendered the contradictory "Penalty Release in 0 days" while the
/// release action was still blocked.
/// </summary>
public class GetPenaltiesRefreshTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<ITransactionService> _mockTransactionService = new();

    public GetPenaltiesRefreshTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The invariant that makes "Penalty Release in 0 days" unrepresentable:
    /// DaysLeftForPenalty reaches 0 if and only if IsExpired is true.
    /// </summary>
    [Theory]
    [InlineData(-48)]   // long expired
    [InlineData(-3)]    // expired, inside where the old calendar-day maths also said 0
    [InlineData(0)]     // exactly at the boundary
    [InlineData(1)]     // under an hour to go
    [InlineData(5)]     // later today
    [InlineData(23)]    // under a day
    [InlineData(25)]    // just over a day
    [InlineData(24 * 9)] // well inside the penalty window
    public async Task RefreshPenalties_ZeroDaysRemainingAlwaysMeansExpired(int hoursUntilExpiry)
    {
        // Arrange
        var lookup = GivenPenaltyExpiringIn(TimeSpan.FromHours(hoursUntilExpiry));
        var handler = CreateHandler();

        // Act
        var result = await handler.RefreshPenalties(new[] { lookup });

        // Assert
        result.IsSuccess.Should().BeTrue();
        (lookup.DaysLeftForPenalty == 0).Should().Be(lookup.IsExpired,
            $"with {hoursUntilExpiry}h to expiry the countdown read {lookup.DaysLeftForPenalty} " +
            $"while IsExpired was {lookup.IsExpired} — the UI would render a countdown of 0 days " +
            "next to a blocked release action");
    }

    [Fact]
    public async Task RefreshPenalties_WhenExpiryIsLaterToday_DoesNotReportZeroDaysRemaining()
    {
        // Arrange — the exact shape that produced "Penalty Release in 0 days": still locked,
        // but on the same calendar day, so day-truncation had already reached zero.
        var lookup = GivenPenaltyExpiringIn(TimeSpan.FromHours(5));
        var handler = CreateHandler();

        // Act
        await handler.RefreshPenalties(new[] { lookup });

        // Assert
        lookup.IsExpired.Should().BeFalse();
        lookup.DaysLeftForPenalty.Should().Be(1, "a partial day remaining must round up, never to 0");
    }

    [Fact]
    public async Task RefreshPenalties_WhenExpired_ReportsZeroDaysRemaining()
    {
        // Arrange
        var lookup = GivenPenaltyExpiringIn(TimeSpan.FromHours(-5));
        var handler = CreateHandler();

        // Act
        await handler.RefreshPenalties(new[] { lookup });

        // Assert
        lookup.IsExpired.Should().BeTrue();
        lookup.DaysLeftForPenalty.Should().Be(0);
    }

    [Fact]
    public async Task RefreshPenalties_WhenProjectInfoMissing_LeavesPenaltyLockedWithMaximumWait()
    {
        // Arrange — unchanged fallback behaviour: without project info the penalty duration is
        // unknown, so the release must not be offered.
        var lookup = GivenPenaltyExpiringIn(TimeSpan.FromHours(-5));
        lookup.Project = null;
        var handler = CreateHandler();

        // Act
        await handler.RefreshPenalties(new[] { lookup });

        // Assert
        lookup.IsExpired.Should().BeFalse();
        lookup.DaysLeftForPenalty.Should().Be(365);
    }

    private GetPenalties.GetPenaltiesHandler CreateHandler()
    {
        return new GetPenalties.GetPenaltiesHandler(
            Mock.Of<IPortfolioService>(),
            Mock.Of<IAngorIndexerService>(),
            _mockTransactionService.Object,
            Mock.Of<IProjectInvestmentsService>(),
            Mock.Of<IProjectService>(),
            _fixture.NetworkConfiguration);
    }

    /// <summary>
    /// Builds a penalty whose timelock expires at now + <paramref name="untilExpiry"/>, by
    /// back-dating the recovery transaction relative to a fixed penalty duration.
    /// </summary>
    private LookupInvestment GivenPenaltyExpiringIn(TimeSpan untilExpiry)
    {
        var penaltyDuration = TimeSpan.FromDays(10);

        // RefreshPenalties adds the buffer on top of the expiry date, so account for it here to
        // make untilExpiry mean "until the release actually becomes available".
        var buffer = Angor.Sdk.Funding.Shared.TimelockSafety.BufferFor(_fixture.NetworkConfiguration);
        var recoveryTimestamp = DateTimeOffset.UtcNow.Add(untilExpiry) - penaltyDuration - buffer;

        const string transactionId = "recovery-tx";

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(transactionId))
            .ReturnsAsync(new QueryTransaction
            {
                Timestamp = recoveryTimestamp.ToUnixTimeSeconds(),
                Outputs = new List<QueryTransactionOutput>()
            });

        return new LookupInvestment
        {
            ProjectIdentifier = "project-1",
            RecoveryTransactionId = transactionId,
            Project = new Project { PenaltyDuration = penaltyDuration }
        };
    }
}

using Angor.Shared.Models;
using App.UI.Shared.Services;
using FluentAssertions;
using Xunit;

namespace App.Test.Integration;

/// <summary>
/// Unit tests for the SDK fee-estimate to UI-preset mapping.
///
/// Regression context: the deploy and invest flows used hardcoded presets
/// (50/20/5 sat/vB) and never called the indexer, so the fee shown and used
/// bore no relation to actual network conditions.
/// </summary>
public class FeeRateProviderMapTests
{
    /// <summary>
    /// MempoolSpaceIndexerApi.GetFeeEstimationAsync returns FeeRate in sat/kB
    /// (fastestFee * 1100) keyed by confirmations 1 / 3 / 6 / 18.
    /// </summary>
    [Fact]
    public void Map_ConvertsSatsPerKilobyteToSatsPerVbyte_AndPicksTheRightTargets()
    {
        // Arrange — mirrors what the indexer returns for 40/20/10/5 sat/vB
        var estimates = new[]
        {
            new FeeEstimation { Confirmations = 1, FeeRate = 40_000 },
            new FeeEstimation { Confirmations = 3, FeeRate = 20_000 },
            new FeeEstimation { Confirmations = 6, FeeRate = 10_000 },
            new FeeEstimation { Confirmations = 18, FeeRate = 5_000 }
        };

        // Act
        var rates = FeeRateProvider.Map(estimates);

        // Assert
        rates.Priority.Should().Be(40);
        rates.Standard.Should().Be(20);
        rates.Economy.Should().Be(5);
    }

    [Fact]
    public void Map_ClampsToOneSatPerVbyte_SoWeNeverBuildBelowRelayMinimum()
    {
        // Arrange — a quiet mainnet: mempool.space reports 1 sat/vB across the board,
        // which the SDK turns into 1 * 1100 = 1100 sat/kB. Integer division of
        // 1100 / 1000 is 1, but anything below 1000 sat/kB would floor to 0.
        var estimates = new[]
        {
            new FeeEstimation { Confirmations = 1, FeeRate = 1_100 },
            new FeeEstimation { Confirmations = 3, FeeRate = 900 },
            new FeeEstimation { Confirmations = 18, FeeRate = 500 }
        };

        // Act
        var rates = FeeRateProvider.Map(estimates);

        // Assert
        rates.Priority.Should().Be(1);
        rates.Standard.Should().Be(1);
        rates.Economy.Should().Be(1);
    }

    [Fact]
    public void Map_FallsBackWhenIndexerReturnsNothingUsable()
    {
        FeeRateProvider.Map(Array.Empty<FeeEstimation>()).Should().Be(FeeRates.Fallback);
        FeeRateProvider.Map(new[] { new FeeEstimation { Confirmations = 1, FeeRate = 0 } })
            .Should().Be(FeeRates.Fallback);
    }

    [Fact]
    public void Map_UsesClosestAvailableTarget_WhenTheIndexerOmitsSomeConfirmations()
    {
        // Arrange — only a single estimate available
        var estimates = new[] { new FeeEstimation { Confirmations = 6, FeeRate = 7_000 } };

        // Act
        var rates = FeeRateProvider.Map(estimates);

        // Assert — all three presets collapse onto the one estimate rather than
        // silently reverting to the hardcoded defaults.
        rates.Should().Be(new FeeRates(7, 7, 7));
    }
}

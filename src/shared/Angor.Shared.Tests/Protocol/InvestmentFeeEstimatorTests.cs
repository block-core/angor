using Angor.Shared.Protocol;
using FluentAssertions;
using Xunit;

namespace Angor.Shared.Tests.Protocol;

public class InvestmentFeeEstimatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EstimateOnChainRequired_AddsPerInputFee_ForEachExtraInput(int inputCount)
    {
        // Arrange
        const long amount = 1_000_000;
        const int feeRate = 20;
        long single = InvestmentFeeEstimator.EstimateOnChainRequired(amount, 1, 3, feeRate, 1);

        // Act
        long actual = InvestmentFeeEstimator.EstimateOnChainRequired(amount, 1, 3, feeRate, inputCount);

        // Assert
        actual.Should().Be(single + ((inputCount - 1) * 68L * feeRate));
    }

    [Fact]
    public void EstimateOnChainRequired_DefaultsToSingleInput()
    {
        InvestmentFeeEstimator.EstimateOnChainRequired(500_000, 1, 2, 20)
            .Should().Be(InvestmentFeeEstimator.EstimateOnChainRequired(500_000, 1, 2, 20, 1));
    }
}
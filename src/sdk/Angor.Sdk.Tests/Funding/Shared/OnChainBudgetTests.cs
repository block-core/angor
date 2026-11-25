using Angor.Sdk.Funding.Shared;
using FluentAssertions;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Shared;

public class OnChainBudgetTests
{
    [Theory]
    [InlineData(1, 10583)]   // 10001 + 252 vB * 1 + 330 dust headroom (matches "required amount: 10583 sats" in user logs)
    [InlineData(3, 11087)]   // 10001 + 252 vB * 3 + 330 (matches "required amount: 11087 sats")
    public void DeployRequiredSats_IncludesCreateFeeMinerFeeAndDustHeadroom(long feeRate, long expected)
    {
        OnChainBudget.DeployRequiredSats(feeRate).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 10253)]
    [InlineData(3, 10757)]
    public void DeployMinimumAcceptableSats_ExcludesDustHeadroom(long feeRate, long expected)
    {
        OnChainBudget.DeployMinimumAcceptableSats(feeRate).Should().Be(expected);
    }

    [Fact]
    public void DeployRequiredSats_IsAboveCreateFeeAlone_SoWalletGateCoversMinerFee()
    {
        // A wallet holding exactly the create fee (10001) cannot build the tx; the gate must demand more.
        OnChainBudget.DeployRequiredSats(1).Should().BeGreaterThan(Angor.Sdk.Common.NetworkConfiguration.AngorCreateFeeSats);
    }
}

using System.Globalization;
using Angor.Client.Services;
using FluentAssertions;

namespace Angor.Client.Tests;

public class InvestHandoffTests
{
    private const string Base = "https://angor.io/invest/abc";

    // --- Network handoff (ApplyNetworkFromQueryString rules) ---

    [Fact]
    public void EvaluateNetwork_WhenSameNetwork_DoesNothing()
    {
        // Act
        NetworkHandoffDecision decision = InvestHandoff.EvaluateNetwork($"{Base}?network=Angornet", "Angornet", false);

        // Assert
        decision.Action.Should().Be(NetworkHandoffAction.None);
    }

    [Fact]
    public void EvaluateNetwork_WhenSameNetworkAndWalletExists_DoesNothing()
    {
        // Act
        NetworkHandoffDecision decision = InvestHandoff.EvaluateNetwork($"{Base}?network=Main", "Main", true);

        // Assert
        decision.Action.Should().Be(NetworkHandoffAction.None);
    }

    [Fact]
    public void EvaluateNetwork_WhenDifferentNetworkAndNoWallet_Switches()
    {
        // Act
        NetworkHandoffDecision decision = InvestHandoff.EvaluateNetwork($"{Base}?network=Main", "Angornet", false);

        // Assert
        decision.Action.Should().Be(NetworkHandoffAction.Switch);
        decision.RequestedNetwork.Should().Be("Main");
    }

    [Fact]
    public void EvaluateNetwork_WhenDifferentNetworkAndWalletExists_RejectsWithoutSwitching()
    {
        // Act
        NetworkHandoffDecision decision = InvestHandoff.EvaluateNetwork($"{Base}?network=Angornet", "Main", true);

        // Assert
        decision.Action.Should().Be(NetworkHandoffAction.Reject);
        decision.RequestedNetwork.Should().Be("Angornet");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?network=")]
    [InlineData("?network=Testnet")]
    [InlineData("?network=main")]
    [InlineData("?network=Main%20")]
    [InlineData("?other=Main")]
    public void EvaluateNetwork_WhenValueMissingOrInvalid_IsIgnored(string query)
    {
        // Act
        NetworkHandoffDecision withWallet = InvestHandoff.EvaluateNetwork(Base + query, "Angornet", true);
        NetworkHandoffDecision withoutWallet = InvestHandoff.EvaluateNetwork(Base + query, "Angornet", false);

        // Assert
        withWallet.Action.Should().Be(NetworkHandoffAction.None);
        withoutWallet.Action.Should().Be(NetworkHandoffAction.None);
    }

    // --- Amount handoff (?amount= rules) ---

    [Theory]
    [InlineData("0.001", 0.001)]
    [InlineData("1", 1)]
    [InlineData("12.5", 12.5)]
    public void ParseRequestedAmount_WhenValid_ReturnsAmount(string raw, double expected)
    {
        // Act
        decimal? amount = InvestHandoff.ParseRequestedAmount(raw, false);

        // Assert
        amount.Should().Be((decimal)expected);
    }

    [Fact]
    public void ParseRequestedAmount_UsesInvariantCulture()
    {
        // Arrange - a culture where the comma is the decimal separator must not change the parse.
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            // Act
            decimal? dotted = InvestHandoff.ParseRequestedAmount("0.5", false);
            decimal? comma = InvestHandoff.ParseRequestedAmount("0,5", false);

            // Assert
            dotted.Should().Be(0.5m);
            comma.Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ParseRequestedAmount_WhenPatternHasFixedAmount_IsIgnored()
    {
        // Act
        decimal? amount = InvestHandoff.ParseRequestedAmount("0.5", true);

        // Assert
        amount.Should().BeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("-1")]
    [InlineData("-0.001")]
    public void ParseRequestedAmount_WhenZeroOrNegative_IsIgnored(string raw)
    {
        // Act
        decimal? amount = InvestHandoff.ParseRequestedAmount(raw, false);

        // Assert
        amount.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1e3")]
    [InlineData("1,000.5")]
    public void ParseRequestedAmount_WhenMissingOrMalformed_IsIgnored(string? raw)
    {
        // Act
        decimal? amount = InvestHandoff.ParseRequestedAmount(raw, false);

        // Assert
        amount.Should().BeNull();
    }
}

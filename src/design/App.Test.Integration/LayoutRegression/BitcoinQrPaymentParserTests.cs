using App.UI.Sections.Funds;
using FluentAssertions;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

public class BitcoinQrPaymentParserTests
{
    private const string Address = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh";

    [Fact]
    public void TryParse_plain_address_returns_address_without_amount()
    {
        BitcoinQrPaymentParser.TryParse(Address, out BitcoinQrPayment? payment, out string? error)
            .Should().BeTrue();

        error.Should().BeNull();
        payment.Should().Be(new BitcoinQrPayment(Address, null));
    }

    [Fact]
    public void TryParse_bip21_address_and_amount_returns_both()
    {
        BitcoinQrPaymentParser.TryParse($"bitcoin:{Address}?amount=0.00123456&label=Angor",
                out BitcoinQrPayment? payment, out string? error)
            .Should().BeTrue();

        error.Should().BeNull();
        payment.Should().Be(new BitcoinQrPayment(Address, 0.00123456m));
    }

    [Fact]
    public void TryParse_bip21_without_amount_returns_address_only()
    {
        BitcoinQrPaymentParser.TryParse($"BITCOIN:{Address}", out BitcoinQrPayment? payment, out _)
            .Should().BeTrue();

        payment.Should().Be(new BitcoinQrPayment(Address, null));
    }

    [Theory]
    [InlineData("lightning:lnbc1test")]
    [InlineData("not-a-bitcoin-address")]
    [InlineData("bitcoin:bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh?amount=not-a-number")]
    [InlineData("bitcoin:bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh?amount=0.000000001")]
    [InlineData("bitcoin:bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh?req-something=value")]
    public void TryParse_unsupported_or_invalid_content_fails(string content)
    {
        BitcoinQrPaymentParser.TryParse(content, out BitcoinQrPayment? payment, out string? error)
            .Should().BeFalse();

        payment.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }
}

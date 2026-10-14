using Angor.Client.Storage;
using Angor.Shared.Models;
using FluentAssertions;
using Moq;

namespace Angor.Client.Tests;

public class FounderKeyAccessTests
{
    private readonly Mock<IWalletStorage> _walletStorage = new();

    [Fact]
    public void HasFounderKeys_WhenNoWallet_ReturnsFalse()
    {
        // Arrange
        _walletStorage.Setup(w => w.HasWallet()).Returns(false);

        // Act
        bool result = FounderKeyAccess.HasFounderKeys(_walletStorage.Object);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasFounderKeys_WhenFounderKeysNull_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        _walletStorage.Setup(w => w.HasWallet()).Returns(true);
        _walletStorage.Setup(w => w.GetFounderKeys()).Returns((FounderKeyCollection)null!);

        // Act
        Func<bool> act = () => FounderKeyAccess.HasFounderKeys(_walletStorage.Object);

        // Assert
        act.Should().NotThrow().Which.Should().BeFalse();
    }

    [Fact]
    public void HasFounderKeys_WhenKeysEmpty_ReturnsFalse()
    {
        // Arrange - what the invest flow stores for browser wallets
        _walletStorage.Setup(w => w.HasWallet()).Returns(true);
        _walletStorage.Setup(w => w.GetFounderKeys()).Returns(new FounderKeyCollection());

        // Act
        bool result = FounderKeyAccess.HasFounderKeys(_walletStorage.Object);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasFounderKeys_WhenKeysPresent_ReturnsTrue()
    {
        // Arrange
        _walletStorage.Setup(w => w.HasWallet()).Returns(true);
        _walletStorage.Setup(w => w.GetFounderKeys())
            .Returns(new FounderKeyCollection { Keys = { new FounderKeys { ProjectIdentifier = "p" } } });

        // Act
        bool result = FounderKeyAccess.HasFounderKeys(_walletStorage.Object);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InvestorOnlyMessage_MatchesRequiredText()
    {
        FounderKeyAccess.InvestorOnlyMessage.Should().Be(
            "This browser wallet is investor-only. Import your recovery phrase into the Angor app to create or manage projects.");
    }
}

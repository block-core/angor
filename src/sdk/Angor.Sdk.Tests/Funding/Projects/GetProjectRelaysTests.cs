using Angor.Sdk.Funding.Projects.Operations;
using Angor.Shared.Services;
using FluentAssertions;
using Moq;
using Nostr.Client.Messages;

namespace Angor.Sdk.Tests.Funding.Projects;

public class GetProjectRelaysTests
{
    private readonly Mock<IRelayService> _mockRelayService;
    private readonly GetProjectRelays.GetProjectRelaysHandler _sut;

    public GetProjectRelaysTests()
    {
        _mockRelayService = new Mock<IRelayService>();
        _sut = new GetProjectRelays.GetProjectRelaysHandler(_mockRelayService.Object);
    }

    [Fact]
    public async Task Handle_WhenPubKeyIsNull_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetProjectRelays.GetProjectRelaysRequest(null!);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelayUrls.Should().BeEmpty();
        _mockRelayService.Verify(
            x => x.LookupRelayListForNPubs(
                It.IsAny<Action<string, List<NostrEventTag>>>(),
                It.IsAny<Action>(),
                It.IsAny<string[]>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPubKeyIsEmpty_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetProjectRelays.GetProjectRelaysRequest("");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelayUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRelaysFound_ReturnsRelayUrls()
    {
        // Arrange
        var nostrPubKey = "npub1abc123";
        var request = new GetProjectRelays.GetProjectRelaysRequest(nostrPubKey);

        _mockRelayService
            .Setup(x => x.LookupRelayListForNPubs(
                It.IsAny<Action<string, List<NostrEventTag>>>(),
                It.IsAny<Action>(),
                nostrPubKey))
            .Callback<Action<string, List<NostrEventTag>>, Action, string[]>((onRelay, onComplete, _) =>
            {
                // Simulate relay list response
                var tags = new List<NostrEventTag>
                {
                    new NostrEventTag("r", "wss://relay1.example.com"),
                    new NostrEventTag("r", "wss://relay2.example.com")
                };
                onRelay(nostrPubKey, tags);
                onComplete();
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelayUrls.Should().Contain("wss://relay1.example.com");
        result.Value.RelayUrls.Should().Contain("wss://relay2.example.com");
    }

    [Fact]
    public async Task Handle_WhenNoRelaysFound_ReturnsEmptyList()
    {
        // Arrange
        var nostrPubKey = "npub1abc123";
        var request = new GetProjectRelays.GetProjectRelaysRequest(nostrPubKey);

        _mockRelayService
            .Setup(x => x.LookupRelayListForNPubs(
                It.IsAny<Action<string, List<NostrEventTag>>>(),
                It.IsAny<Action>(),
                nostrPubKey))
            .Callback<Action<string, List<NostrEventTag>>, Action, string[]>((_, onComplete, __) =>
            {
                // No relays found, just complete
                onComplete();
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelayUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenRelayTagHasEmptyUrl_FiltersItOut()
    {
        // Arrange
        var nostrPubKey = "npub1abc123";
        var request = new GetProjectRelays.GetProjectRelaysRequest(nostrPubKey);

        _mockRelayService
            .Setup(x => x.LookupRelayListForNPubs(
                It.IsAny<Action<string, List<NostrEventTag>>>(),
                It.IsAny<Action>(),
                nostrPubKey))
            .Callback<Action<string, List<NostrEventTag>>, Action, string[]>((onRelay, onComplete, _) =>
            {
                var tags = new List<NostrEventTag>
                {
                    new NostrEventTag("r", "wss://relay1.example.com"),
                    new NostrEventTag("r"), // empty/no additional data
                    new NostrEventTag("r", "wss://relay3.example.com")
                };
                onRelay(nostrPubKey, tags);
                onComplete();
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelayUrls.Should().HaveCount(2);
        result.Value.RelayUrls.Should().Contain("wss://relay1.example.com");
        result.Value.RelayUrls.Should().Contain("wss://relay3.example.com");
    }
}

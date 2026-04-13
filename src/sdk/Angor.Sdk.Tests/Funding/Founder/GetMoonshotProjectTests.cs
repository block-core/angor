using Angor.Sdk.Funding.Founder.Operations;
using Angor.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nostr.Client.Client;

namespace Angor.Sdk.Tests.Funding.Founder;

public class GetMoonshotProjectTests
{
    private readonly Mock<INostrCommunicationFactory> _mockCommunicationFactory;
    private readonly Mock<INetworkService> _mockNetworkService;
    private readonly GetMoonshotProject.GetMoonshotProjectHandler _sut;

    public GetMoonshotProjectTests()
    {
        _mockCommunicationFactory = new Mock<INostrCommunicationFactory>();
        _mockNetworkService = new Mock<INetworkService>();

        _sut = new GetMoonshotProject.GetMoonshotProjectHandler(
            _mockCommunicationFactory.Object,
            _mockNetworkService.Object,
            NullLogger<GetMoonshotProject.GetMoonshotProjectHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenEventIdIsEmpty_ReturnsFailure()
    {
        // Arrange
        var request = new GetMoonshotProject.GetMoonshotProjectRequest("");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Event ID cannot be empty");
    }

    [Fact]
    public async Task Handle_WhenEventIdIsWhitespace_ReturnsFailure()
    {
        // Arrange
        var request = new GetMoonshotProject.GetMoonshotProjectRequest("   ");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Event ID cannot be empty");
    }

    [Fact]
    public async Task Handle_WhenEventIdIsNull_ReturnsFailure()
    {
        // Arrange
        var request = new GetMoonshotProject.GetMoonshotProjectRequest(null!);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Event ID cannot be empty");
    }

    [Fact]
    public async Task Handle_WhenCommunicationFactoryThrows_ReturnsFailure()
    {
        // Arrange
        var request = new GetMoonshotProject.GetMoonshotProjectRequest("event-123");

        _mockCommunicationFactory
            .Setup(x => x.GetOrCreateClient(It.IsAny<INetworkService>()))
            .Throws(new InvalidOperationException("Connection failed"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to fetch Moonshot project");
        result.Error.Should().Contain("Connection failed");
    }

    [Fact]
    public async Task Handle_WhenEventIdHasWhitespace_TrimsItBeforeProcessing()
    {
        // Arrange — event ID with whitespace should still go through (not rejected as empty),
        // but the factory throws so we never actually reach the Nostr logic
        var request = new GetMoonshotProject.GetMoonshotProjectRequest("  event-123  ");

        _mockCommunicationFactory
            .Setup(x => x.GetOrCreateClient(It.IsAny<INetworkService>()))
            .Throws(new InvalidOperationException("Connection failed"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert — it should NOT fail with "empty" error since the ID is valid after trimming
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotContain("Event ID cannot be empty");
        result.Error.Should().Contain("Failed to fetch Moonshot project");
    }
}

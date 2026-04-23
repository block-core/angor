using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nostr.Client.Responses;

namespace Angor.Sdk.Tests.Funding.Founder;

public class CreateProjectProfileTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<IRelayService> _mockRelayService;
    private readonly Mock<IGenericDocumentCollection<DerivedProjectKeys>> _mockDerivedProjectKeysCollection;
    private readonly Mock<ILogger<CreateProjectProfile.CreateProjectProfileHandler>> _mockLogger;
    private readonly CreateProjectProfile.CreateProjectProfileHandler _sut;

    public CreateProjectProfileTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockRelayService = new Mock<IRelayService>();
        _mockDerivedProjectKeysCollection = new Mock<IGenericDocumentCollection<DerivedProjectKeys>>();
        _mockLogger = new Mock<ILogger<CreateProjectProfile.CreateProjectProfileHandler>>();

        _sut = new CreateProjectProfile.CreateProjectProfileHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockAngorIndexerService.Object,
            _mockRelayService.Object,
            _mockDerivedProjectKeysCollection.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRequest();

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Wallet locked");
    }

    [Fact]
    public async Task Handle_WhenRelayRejectsProfile_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSeedwords();
        SetupDerivation();

        _mockRelayService
            .Setup(x => x.CreateNostrProfileAsync(
                It.IsAny<Nostr.Client.Messages.Metadata.NostrMetadata>(),
                It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns<Nostr.Client.Messages.Metadata.NostrMetadata, string, Action<NostrOkResponse>>(
                (metadata, key, callback) =>
                {
                    // Simulate relay rejection
                    callback(new NostrOkResponse
                    {
                        Accepted = false,
                        Message = "Rate limited"
                    });
                    return Task.FromResult("event-id");
                });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to store the project profile on the relay");
    }

    [Fact]
    public async Task Handle_WhenRelayAcceptsAndNip65Accepted_ReturnsSuccess()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSeedwords();
        SetupDerivation();

        _mockRelayService
            .Setup(x => x.CreateNostrProfileAsync(
                It.IsAny<Nostr.Client.Messages.Metadata.NostrMetadata>(),
                It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns<Nostr.Client.Messages.Metadata.NostrMetadata, string, Action<NostrOkResponse>>(
                (metadata, key, callback) =>
                {
                    callback(new NostrOkResponse
                    {
                        Accepted = true,
                        EventId = "profile-event-id"
                    });
                    return Task.FromResult("profile-event-id");
                });

        _mockRelayService
            .Setup(x => x.PublishNip65List(
                It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns<string, Action<NostrOkResponse>>((key, callback) =>
            {
                callback(new NostrOkResponse
                {
                    Accepted = true,
                    EventId = "nip65-event-id"
                });
                return "nip65-event-id";
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().Be("profile-event-id");
    }

    [Fact]
    public async Task Handle_WhenNip65ListFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSeedwords();
        SetupDerivation();

        _mockRelayService
            .Setup(x => x.CreateNostrProfileAsync(
                It.IsAny<Nostr.Client.Messages.Metadata.NostrMetadata>(),
                It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns<Nostr.Client.Messages.Metadata.NostrMetadata, string, Action<NostrOkResponse>>(
                (metadata, key, callback) =>
                {
                    callback(new NostrOkResponse
                    {
                        Accepted = true,
                        EventId = "profile-event-id"
                    });
                    return Task.FromResult("profile-event-id");
                });

        _mockRelayService
            .Setup(x => x.PublishNip65List(
                It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns<string, Action<NostrOkResponse>>((key, callback) =>
            {
                callback(new NostrOkResponse
                {
                    Accepted = false,
                    Message = "NIP-65 rejected"
                });
                return "nip65-event-id";
            });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to publish NIP-65 list");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_DeriveNostrKeyFromFounderKey()
    {
        // Arrange
        var request = CreateValidRequest();
        SetupSeedwords();
        SetupDerivation();

        _mockRelayService
            .Setup(x => x.CreateNostrProfileAsync(
                It.IsAny<Nostr.Client.Messages.Metadata.NostrMetadata>(),
                It.IsAny<string>(),
                It.IsAny<Action<NostrOkResponse>>()))
            .Returns<Nostr.Client.Messages.Metadata.NostrMetadata, string, Action<NostrOkResponse>>(
                (metadata, key, callback) =>
                {
                    callback(new NostrOkResponse { Accepted = true, EventId = "eid" });
                    return Task.FromResult("eid");
                });

        _mockRelayService
            .Setup(x => x.PublishNip65List(It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Returns<string, Action<NostrOkResponse>>((key, callback) =>
            {
                callback(new NostrOkResponse { Accepted = true });
                return "nip65";
            });

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockDerivationOperations.Verify(
            x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), "founder-key-abc"),
            Times.Once);
    }

    private static CreateProjectProfile.CreateProjectProfileRequest CreateValidRequest()
    {
        return new CreateProjectProfile.CreateProjectProfileRequest(
            new WalletId("wallet-1"),
            new ProjectSeedDto("founder-key-abc", "recovery-key", "nostr-pub-key", "project-id"),
            new CreateProjectDto
            {
                ProjectName = "Test Project",
                Description = "A test project",
                AvatarUri = "https://example.com/avatar.png",
                BannerUri = "https://example.com/banner.png",
                Sats = 1_000_000,
                StartDate = DateTime.UtcNow,
                TargetAmount = new Amount(1_000_000),
                PenaltyDays = 30,
                Stages = new List<CreateProjectStageDto>
                {
                    new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), 50),
                    new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(2)), 50)
                }
            });
    }

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));
    }

    private void SetupDerivation()
    {
        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());
    }
}

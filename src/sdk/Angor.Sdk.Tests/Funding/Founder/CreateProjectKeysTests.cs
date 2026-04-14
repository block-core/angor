using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Sdk.Tests.Funding.Founder;

public class CreateProjectKeysTests
{
    private readonly Mock<IGenericDocumentCollection<DerivedProjectKeys>> _mockDerivedKeysCollection;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly CreateProjectKeys.CreateProjectKeysHandler _sut;

    public CreateProjectKeysTests()
    {
        _mockDerivedKeysCollection = new Mock<IGenericDocumentCollection<DerivedProjectKeys>>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        var logger = NullLoggerFactory.Instance.CreateLogger<CreateProjectKeys.CreateProjectKeysHandler>();

        _sut = new CreateProjectKeys.CreateProjectKeysHandler(
            _mockDerivedKeysCollection.Object,
            _mockAngorIndexerService.Object,
            logger);
    }

    [Fact]
    public async Task Handle_WhenKeysNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new CreateProjectKeys.CreateProjectKeysRequest(walletId);

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync(walletId.ToString()))
            .ReturnsAsync(Result.Failure<DerivedProjectKeys>("Not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project keys not found");
    }

    [Fact]
    public async Task Handle_WhenKeysDocumentIsNull_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new CreateProjectKeys.CreateProjectKeysRequest(walletId);

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync(walletId.ToString()))
            .ReturnsAsync(Result.Success<DerivedProjectKeys>(null!));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project keys not found");
    }

    [Fact]
    public async Task Handle_WhenAllSlotsOccupied_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new CreateProjectKeys.CreateProjectKeysRequest(walletId);

        var keys = Enumerable.Range(0, 3).Select(i => new FounderKeys
        {
            FounderKey = $"founder-key-{i}",
            FounderRecoveryKey = $"recovery-key-{i}",
            NostrPubKey = $"nostr-pub-{i}",
            ProjectIdentifier = $"project-id-{i}"
        }).ToList();

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync(walletId.ToString()))
            .ReturnsAsync(Result.Success(new DerivedProjectKeys
            {
                WalletId = walletId.Value,
                Keys = keys
            }));

        // All slots return existing projects
        _mockAngorIndexerService
            .Setup(x => x.GetProjectByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProjectIndexerData());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No available project slot");
    }

    [Fact]
    public async Task Handle_WhenFirstSlotAvailable_ReturnsFirstSlot()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new CreateProjectKeys.CreateProjectKeysRequest(walletId);

        var keys = new List<FounderKeys>
        {
            new FounderKeys
            {
                FounderKey = "founder-key-0",
                FounderRecoveryKey = "recovery-key-0",
                NostrPubKey = "nostr-pub-0",
                ProjectIdentifier = "project-id-0"
            },
            new FounderKeys
            {
                FounderKey = "founder-key-1",
                FounderRecoveryKey = "recovery-key-1",
                NostrPubKey = "nostr-pub-1",
                ProjectIdentifier = "project-id-1"
            }
        };

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync(walletId.ToString()))
            .ReturnsAsync(Result.Success(new DerivedProjectKeys
            {
                WalletId = walletId.Value,
                Keys = keys
            }));

        // First slot is available (no project found)
        _mockAngorIndexerService
            .Setup(x => x.GetProjectByIdAsync("project-id-0"))
            .ReturnsAsync((ProjectIndexerData?)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectSeedDto.FounderKey.Should().Be("founder-key-0");
        result.Value.ProjectSeedDto.FounderRecoveryKey.Should().Be("recovery-key-0");
        result.Value.ProjectSeedDto.NostrPubKey.Should().Be("nostr-pub-0");
        result.Value.ProjectSeedDto.ProjectIdentifier.Should().Be("project-id-0");
    }

    [Fact]
    public async Task Handle_WhenFirstSlotOccupied_ReturnsSecondSlot()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new CreateProjectKeys.CreateProjectKeysRequest(walletId);

        var keys = new List<FounderKeys>
        {
            new FounderKeys
            {
                FounderKey = "founder-key-0",
                FounderRecoveryKey = "recovery-key-0",
                NostrPubKey = "nostr-pub-0",
                ProjectIdentifier = "project-id-0"
            },
            new FounderKeys
            {
                FounderKey = "founder-key-1",
                FounderRecoveryKey = "recovery-key-1",
                NostrPubKey = "nostr-pub-1",
                ProjectIdentifier = "project-id-1"
            }
        };

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync(walletId.ToString()))
            .ReturnsAsync(Result.Success(new DerivedProjectKeys
            {
                WalletId = walletId.Value,
                Keys = keys
            }));

        // First slot is occupied
        _mockAngorIndexerService
            .Setup(x => x.GetProjectByIdAsync("project-id-0"))
            .ReturnsAsync(new ProjectIndexerData());

        // Second slot is available
        _mockAngorIndexerService
            .Setup(x => x.GetProjectByIdAsync("project-id-1"))
            .ReturnsAsync((ProjectIndexerData?)null);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectSeedDto.FounderKey.Should().Be("founder-key-1");
        result.Value.ProjectSeedDto.ProjectIdentifier.Should().Be("project-id-1");
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_StopsSearching()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new CreateProjectKeys.CreateProjectKeysRequest(walletId);

        var keys = Enumerable.Range(0, 5).Select(i => new FounderKeys
        {
            FounderKey = $"founder-key-{i}",
            FounderRecoveryKey = $"recovery-key-{i}",
            NostrPubKey = $"nostr-pub-{i}",
            ProjectIdentifier = $"project-id-{i}"
        }).ToList();

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync(walletId.ToString()))
            .ReturnsAsync(Result.Success(new DerivedProjectKeys
            {
                WalletId = walletId.Value,
                Keys = keys
            }));

        // All slots occupied
        _mockAngorIndexerService
            .Setup(x => x.GetProjectByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProjectIndexerData());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.Handle(request, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No available project slot");
    }
}

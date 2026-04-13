using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class CheckForReleaseSignaturesTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<ISignService> _mockSignService;
    private readonly CheckForReleaseSignatures.CheckForReleaseSignaturesHandler _sut;

    public CheckForReleaseSignaturesTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockProjectService = new Mock<IProjectService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockSignService = new Mock<ISignService>();

        _sut = new CheckForReleaseSignatures.CheckForReleaseSignaturesHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockSignService.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(
            new WalletId("wallet-1"), new ProjectId("project-1"));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task Handle_WhenPortfolioLookupFails_ReturnsFailure()
    {
        // Arrange
        var request = new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(
            new WalletId("wallet-1"), new ProjectId("project-1"));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Storage error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task Handle_WhenNoInvestmentFound_ReturnsFailure()
    {
        // Arrange
        var request = new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(
            new WalletId("wallet-1"), new ProjectId("project-1"));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        var records = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No investment found");
    }

    [Fact]
    public async Task Handle_WhenRequestEventIdIsNull_ReturnsFalse()
    {
        // Arrange
        var request = new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(
            new WalletId("wallet-1"), new ProjectId("project-1"));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            RequestEventId = null
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasReleaseSignatures.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(
            new WalletId("wallet-1"), new ProjectId("project-1"));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            RequestEventId = "event-123"
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Wallet locked");
    }

    private static Project CreateTestProject()
    {
        return new Project
        {
            Id = new ProjectId("project-1"),
            Name = "Test Project",
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub-key",
            ShortDescription = "Test",
            TargetAmount = 1_000_000,
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            EndDate = DateTime.UtcNow.AddYears(1),
            Stages = new List<Stage>()
        };
    }
}

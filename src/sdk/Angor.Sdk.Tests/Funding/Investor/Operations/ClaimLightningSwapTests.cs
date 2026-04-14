using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using Angor.Shared.Models;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class ClaimLightningSwapTests
{
    private readonly Mock<IBoltzClaimService> _mockBoltzClaimService;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ILogger<ClaimLightningSwap.ClaimLightningSwapByIdHandler>> _mockLogger;
    private readonly ClaimLightningSwap.ClaimLightningSwapByIdHandler _sut;

    public ClaimLightningSwapTests()
    {
        _mockBoltzClaimService = new Mock<IBoltzClaimService>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockLogger = new Mock<ILogger<ClaimLightningSwap.ClaimLightningSwapByIdHandler>>();

        _sut = new ClaimLightningSwap.ClaimLightningSwapByIdHandler(
            _mockBoltzClaimService.Object,
            _mockSwapStorageService.Object,
            _mockProjectService.Object,
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenSwapNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Failure<BoltzSwapDocument>("Swap not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Swap not found");
    }

    [Fact]
    public async Task Handle_WhenSwapHasNoProjectId_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: null);
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no associated project ID");
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == "project-1")))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task Handle_WhenFounderKeyIsEmpty_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        var project = CreateTestProject();
        project.FounderKey = "";
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Founder key is required");
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get wallet data");
    }

    [Fact]
    public async Task Handle_WhenNoLockupTxHex_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123", LockupTransactionHex: null);

        var swapDoc = CreateSwapDocument(projectId: "project-1", lockupTxHex: null);
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        SetupSuccessfulKeyDerivation();

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Lockup transaction hex not available");
    }

    [Fact]
    public async Task Handle_WhenClaimFails_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123", LockupTransactionHex: "lockup-hex-abc");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        SetupSuccessfulKeyDerivation();

        _mockBoltzClaimService
            .Setup(x => x.ClaimSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), "lockup-hex-abc", 0, 2))
            .ReturnsAsync(Result.Failure<BoltzClaimResult>("Claim failed: invalid preimage"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Claim failed");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsClaimTransactionDetails()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123", LockupTransactionHex: "lockup-hex-abc");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        SetupSuccessfulKeyDerivation();

        var claimResult = new BoltzClaimResult("claim-tx-id", "claim-tx-hex");
        _mockBoltzClaimService
            .Setup(x => x.ClaimSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), "lockup-hex-abc", 0, 2))
            .ReturnsAsync(Result.Success(claimResult));

        _mockSwapStorageService
            .Setup(x => x.MarkSwapClaimedAsync("swap-123", "wallet-1", "claim-tx-id"))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ClaimTransactionId.Should().Be("claim-tx-id");
        result.Value.ClaimTransactionHex.Should().Be("claim-tx-hex");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_MarksSwapAsClaimed()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123", LockupTransactionHex: "lockup-hex-abc");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        SetupSuccessfulKeyDerivation();

        var claimResult = new BoltzClaimResult("claim-tx-id", "claim-tx-hex");
        _mockBoltzClaimService
            .Setup(x => x.ClaimSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<long>()))
            .ReturnsAsync(Result.Success(claimResult));

        _mockSwapStorageService
            .Setup(x => x.MarkSwapClaimedAsync("swap-123", "wallet-1", "claim-tx-id"))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSwapStorageService.Verify(
            x => x.MarkSwapClaimedAsync("swap-123", "wallet-1", "claim-tx-id"),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLockupTxFromDoc_UsesDocumentHex()
    {
        // Arrange — no LockupTransactionHex in request, but doc has one
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123", LockupTransactionHex: null);

        var swapDoc = CreateSwapDocument(projectId: "project-1", lockupTxHex: "doc-lockup-hex");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        SetupSuccessfulKeyDerivation();

        var claimResult = new BoltzClaimResult("claim-tx-id", "claim-tx-hex");
        _mockBoltzClaimService
            .Setup(x => x.ClaimSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), "doc-lockup-hex", 0, 2))
            .ReturnsAsync(Result.Success(claimResult));

        _mockSwapStorageService
            .Setup(x => x.MarkSwapClaimedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    private void SetupSuccessfulKeyDerivation()
    {
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockDerivationOperations
            .Setup(x => x.DeriveInvestorPrivateKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns(new Key());
    }

    private static BoltzSwapDocument CreateSwapDocument(string? projectId, string? lockupTxHex = "lockup-hex")
    {
        return new BoltzSwapDocument
        {
            SwapId = "swap-123",
            WalletId = "wallet-1",
            ProjectId = projectId,
            LockupTransactionHex = lockupTxHex,
            Invoice = "lnbc1...",
            Address = "bc1...",
            LockupAddress = "bc1lock...",
            Preimage = "preimage-hex",
            PreimageHash = "preimage-hash",
            ClaimPublicKey = "claim-pub",
            RefundPublicKey = "refund-pub",
            SwapTree = "{}",
            ExpectedAmount = 100_000,
            InvoiceAmount = 105_000
        };
    }

    private static Project CreateTestProject()
    {
        return new Project
        {
            Id = new ProjectId("project-1"),
            Name = "Test Project",
            FounderKey = "founder-key-abc",
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

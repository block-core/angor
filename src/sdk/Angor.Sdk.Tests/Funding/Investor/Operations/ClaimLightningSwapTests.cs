using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class ClaimLightningSwapTests
{
    private readonly Mock<IBoltzClaimService> _mockBoltzClaimService;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IHdOperations> _mockHdOperations;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly Mock<ILogger<ClaimLightningSwap.ClaimLightningSwapByIdHandler>> _mockLogger;
    private readonly ClaimLightningSwap.ClaimLightningSwapByIdHandler _sut;

    public ClaimLightningSwapTests()
    {
        _mockBoltzClaimService = new Mock<IBoltzClaimService>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockHdOperations = new Mock<IHdOperations>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        _mockLogger = new Mock<ILogger<ClaimLightningSwap.ClaimLightningSwapByIdHandler>>();

        _sut = new ClaimLightningSwap.ClaimLightningSwapByIdHandler(
            _mockBoltzClaimService.Object,
            _mockSwapStorageService.Object,
            _mockSeedwordsProvider.Object,
            _mockHdOperations.Object,
            _mockWalletAccountBalanceService.Object,
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
    public async Task Handle_WhenSwapHasNoAddress_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        swapDoc.Address = "";
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no receive address");
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
    public async Task Handle_WhenAccountBalanceServiceFails_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Failed to get account info"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get account info");
    }

    [Fact]
    public async Task Handle_WhenAddressNotFoundInWallet_ReturnsFailure()
    {
        // Arrange
        var request = new ClaimLightningSwap.ClaimLightningSwapByIdRequest(
            new WalletId("wallet-1"), "swap-123");

        var swapDoc = CreateSwapDocument(projectId: "project-1");
        _mockSwapStorageService
            .Setup(x => x.GetSwapForWalletAsync("swap-123", "wallet-1"))
            .ReturnsAsync(Result.Success(swapDoc));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));

        // AccountInfo with no addresses matching "bc1..."
        var accountBalanceInfo = new AccountBalanceInfo();
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalanceInfo));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found in wallet");
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
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(sensitiveData));

        var accountInfo = new AccountInfo();
        accountInfo.AddressesInfo.Add(new AddressInfo { Address = "bc1...", HdPath = "m/84'/0'/0'/0/0" });
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalanceInfo));

        _mockHdOperations
            .Setup(x => x.DerivePrivateKey(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
            .Returns("derived-private-key-hex");
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
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Services;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Unit tests for CreateLightningSwap handler (generic, address-based claim key)
/// </summary>
public class CreateLightningSwapTests
{
    private readonly Mock<IBoltzSwapService> _mockBoltzService;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<ILogger<CreateLightningSwap.CreateLightningSwapHandler>> _mockLogger;
    private readonly CreateLightningSwap.CreateLightningSwapHandler _handler;

    public CreateLightningSwapTests()
    {
        _mockBoltzService = new Mock<IBoltzSwapService>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockLogger = new Mock<ILogger<CreateLightningSwap.CreateLightningSwapHandler>>();

        _handler = new CreateLightningSwap.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockSwapStorageService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CreateSwap_WithValidAmount_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var claimPubKey = "02abc123claimkey456789";
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        var expectedSwap = new BoltzSubmarineSwap
        {
            Id = "swap-123",
            Invoice = "lnbc1000n1...",
            Address = address,
            ExpectedAmount = 150000, // delivers more than the ~102k the handler needs
            Status = SwapState.Created
        };

        _mockBoltzService
            .Setup(x => x.GetReverseSwapFeesAsync())
            .ReturnsAsync(Result.Success(new BoltzSwapFees { MinAmount = 25000, MaxAmount = 25000000 }));

        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(amount.Sats);

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedSwap));

        _mockSwapStorageService
            .Setup(x => x.SaveSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(Result.Success());

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            walletId, claimPubKey, amount, address, StageCount: 4);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("swap-123", result.Value.Swap.Id);
        Assert.Equal("lnbc1000n1...", result.Value.Swap.Invoice);
    }

    [Fact]
    public async Task CreateSwap_BoltzServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var claimPubKey = "02abc123claimkey456789";
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        _mockBoltzService
            .Setup(x => x.GetReverseSwapFeesAsync())
            .ReturnsAsync(Result.Success(new BoltzSwapFees { MinAmount = 25000, MaxAmount = 25000000 }));

        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(amount.Sats);

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<BoltzSubmarineSwap>("Boltz API error"));

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            walletId, claimPubKey, amount, address, StageCount: 4);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Boltz API error", result.Error);
    }

    [Fact]
    public async Task CreateSwap_AmountBelowMinimum_BumpsToMinimum()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var claimPubKey = "02abc123claimkey456789";
        var amount = new Amount(10000); // Below 50K minimum
        var address = "bc1qtest123";

        _mockBoltzService
            .Setup(x => x.GetReverseSwapFeesAsync())
            .ReturnsAsync(Result.Success(new BoltzSwapFees { MinAmount = 50000, MaxAmount = 25000000 }));

        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.Is<long>(a => a >= 50000)))
            .ReturnsAsync(51000L);

        var expectedSwap = new BoltzSubmarineSwap
        {
            Id = "swap-bumped",
            Invoice = "lnbc510n1...",
            Address = address,
            ExpectedAmount = 50000,
            Status = SwapState.Created
        };

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, 51000L, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedSwap));

        _mockSwapStorageService
            .Setup(x => x.SaveSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(Result.Success());

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            walletId, claimPubKey, amount, address, StageCount: 0);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        // Verify CalculateInvoiceAmountAsync was called with at least the minimum
        _mockBoltzService.Verify(
            x => x.CalculateInvoiceAmountAsync(It.Is<long>(a => a >= 50000)),
            Times.Once);
    }

    [Fact]
    public async Task CreateSwap_NoProjectIdRequired()
    {
        // Arrange — no project service, no seed provider, no derivation ops needed
        var walletId = new WalletId("test-wallet");
        var claimPubKey = "02abc123claimkey456789";
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        _mockBoltzService
            .Setup(x => x.GetReverseSwapFeesAsync())
            .ReturnsAsync(Result.Success(new BoltzSwapFees { MinAmount = 25000, MaxAmount = 25000000 }));

        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(amount.Sats);

        var expectedSwap = new BoltzSubmarineSwap
        {
            Id = "swap-generic",
            Invoice = "lnbc1000n1...",
            Address = address,
            ExpectedAmount = 150000,
            Status = SwapState.Created
        };

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedSwap));

        _mockSwapStorageService
            .Setup(x => x.SaveSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(Result.Success());

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            walletId, claimPubKey, amount, address, StageCount: 4);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert — handler has no IProjectService, ISeedwordsProvider, or IDerivationOperations
        Assert.True(result.IsSuccess);
    }

    private void SetupFees(long invoiceAmount)
    {
        _mockBoltzService
            .Setup(x => x.GetReverseSwapFeesAsync())
            .ReturnsAsync(Result.Success(new BoltzSwapFees { MinAmount = 1000, MaxAmount = 25000000 }));
        _mockBoltzService
            .Setup(x => x.CalculateInvoiceAmountAsync(It.IsAny<long>()))
            .ReturnsAsync(invoiceAmount);
        _mockSwapStorageService
            .Setup(x => x.SaveSwapAsync(It.IsAny<BoltzSubmarineSwap>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task CreateSwap_WhenBoltzDeliversLessThanNeeded_RecreatesWithBiggerInvoice()
    {
        // Arrange - deploy flow: target 11322 + claim fee 155*3 = 11787 needed on-chain
        const string address = "bc1qtest123";
        SetupFees(11515);
        var short1 = new BoltzSubmarineSwap { Id = "short", Invoice = "ln1", Address = address, ExpectedAmount = 11787 - 47 };
        var ok = new BoltzSubmarineSwap { Id = "ok", Invoice = "ln2", Address = address, ExpectedAmount = 11900 };
        _mockBoltzService.Setup(x => x.CreateSubmarineSwapAsync(address, 11515, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(short1));
        _mockBoltzService.Setup(x => x.CreateSubmarineSwapAsync(address, 11515 + 47 + 10, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(ok));

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            new WalletId("w"), "02abc", new Amount(10001), address, StageCount: 0,
            EstimatedFeeRateSatsPerVbyte: 3, OnChainTargetSats: 11322);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value.Swap.Id);
        _mockBoltzService.Verify(x => x.CreateSubmarineSwapAsync(address, It.IsAny<long>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateSwap_WhenBoltzStillShortAfterBump_ReturnsFailure()
    {
        const string address = "bc1qtest123";
        SetupFees(11515);
        var shortSwap = new BoltzSubmarineSwap { Id = "short", Invoice = "ln1", Address = address, ExpectedAmount = 1000 };
        _mockBoltzService.Setup(x => x.CreateSubmarineSwapAsync(address, It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success(shortSwap));

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            new WalletId("w"), "02abc", new Amount(10001), address, StageCount: 0,
            EstimatedFeeRateSatsPerVbyte: 3, OnChainTargetSats: 11322);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("pay on-chain", result.Error);
    }

    [Fact]
    public async Task CreateSwap_WithOnChainTarget_UsesTargetPlusClaimFeeWithoutAngorFee()
    {
        const string address = "bc1qtest123";
        SetupFees(20000);
        _mockBoltzService.Setup(x => x.CreateSubmarineSwapAsync(address, 20000, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(new BoltzSubmarineSwap { Id = "s", Invoice = "ln", Address = address, ExpectedAmount = 99999 }));

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            new WalletId("w"), "02abc", new Amount(10001), address, StageCount: 0,
            EstimatedFeeRateSatsPerVbyte: 3, OnChainTargetSats: 11322);

        await _handler.Handle(request, CancellationToken.None);

        // 11322 + 155 vB * 3 sat/vB = 11787; the 1% angor fee (100) and spend tx fee must not be added
        _mockBoltzService.Verify(x => x.CalculateInvoiceAmountAsync(11787), Times.Once);
    }
}

/// <summary>
/// Unit tests for MonitorLightningSwap handler
/// </summary>
public class MonitorLightningSwapTests
{
    private readonly Mock<IBoltzWebSocketClient> _mockWebSocketClient;
    private readonly Mock<IBoltzSwapStorageService> _mockSwapStorageService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>> _mockLogger;
    private readonly MonitorLightningSwap.MonitorLightningSwapHandler _handler;

    public MonitorLightningSwapTests()
    {
        _mockWebSocketClient = new Mock<IBoltzWebSocketClient>();
        _mockSwapStorageService = new Mock<IBoltzSwapStorageService>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>>();

        _handler = new MonitorLightningSwap.MonitorLightningSwapHandler(
            _mockWebSocketClient.Object,
            _mockSwapStorageService.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenSwapAlreadyClaimed_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";

        var completedStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.TransactionClaimed,
            TransactionId = "tx123"
        };

        _mockWebSocketClient
            .Setup(x => x.MonitorSwapAsync(swapId, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(completedStatus));

        _mockSwapStorageService
            .Setup(x => x.UpdateSwapStatusAsync(swapId, walletId.Value, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(walletId, swapId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("tx123", result.Value.ClaimTransactionId);
    }

    [Fact]
    public async Task Handle_WhenSwapFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";

        _mockWebSocketClient
            .Setup(x => x.MonitorSwapAsync(swapId, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<BoltzSwapStatus>("Swap expired"));

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(walletId, swapId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error);
    }

    [Fact]
    public async Task Handle_WhenFundsLocked_ClaimsAndReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";

        var lockedStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.TransactionMempool,
            TransactionId = "lockup-tx-123",
            TransactionHex = "0100..."
        };

        _mockWebSocketClient
            .Setup(x => x.MonitorSwapAsync(swapId, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(lockedStatus));

        _mockSwapStorageService
            .Setup(x => x.UpdateSwapStatusAsync(swapId, walletId.Value, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        _mockSwapStorageService
            .Setup(x => x.MarkSwapClaimedAsync(swapId, walletId.Value, It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var claimResponse = new ClaimLightningSwap.ClaimLightningSwapResponse("claim-tx-123", "signed-hex");
        _mockMediator
            .Setup(x => x.Send(It.IsAny<ClaimLightningSwap.ClaimLightningSwapByIdRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(claimResponse));

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(walletId, swapId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("claim-tx-123", result.Value.ClaimTransactionId);

        _mockMediator.Verify(x => x.Send(
            It.Is<ClaimLightningSwap.ClaimLightningSwapByIdRequest>(r => r.SwapId == swapId),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSwapStorageService.Verify(x => x.MarkSwapClaimedAsync(swapId, walletId.Value, "claim-tx-123"), Times.Once);
    }
}

/// <summary>
/// Unit tests for SwapState extension methods
/// </summary>
public class SwapStateExtensionTests
{
    [Theory]
    [InlineData(SwapState.TransactionClaimed, true)]
    [InlineData(SwapState.InvoicePaid, false)]
    [InlineData(SwapState.TransactionMempool, false)]
    [InlineData(SwapState.Created, false)]
    public void IsComplete_ReturnsExpectedValue(SwapState state, bool expected)
    {
        Assert.Equal(expected, state.IsComplete());
    }

    [Theory]
    [InlineData(SwapState.SwapExpired, true)]
    [InlineData(SwapState.InvoiceExpired, true)]
    [InlineData(SwapState.InvoiceFailedToPay, true)]
    [InlineData(SwapState.TransactionRefunded, true)]
    [InlineData(SwapState.InvoicePaid, false)]
    [InlineData(SwapState.TransactionClaimed, false)]
    public void IsFailed_ReturnsExpectedValue(SwapState state, bool expected)
    {
        Assert.Equal(expected, state.IsFailed());
    }

    [Theory]
    [InlineData(SwapState.Created, true)]
    [InlineData(SwapState.InvoicePaid, true)]
    [InlineData(SwapState.TransactionMempool, true)]
    [InlineData(SwapState.TransactionConfirmed, true)]
    [InlineData(SwapState.TransactionClaimed, false)]
    [InlineData(SwapState.SwapExpired, false)]
    public void IsPending_ReturnsExpectedValue(SwapState state, bool expected)
    {
        Assert.Equal(expected, state.IsPending());
    }
}

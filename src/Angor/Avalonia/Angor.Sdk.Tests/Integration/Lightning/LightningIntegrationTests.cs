using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Unit tests for Boltz submarine swap integration
/// </summary>
public class BoltzSwapTests
{
    private readonly Mock<IBoltzSwapService> _mockBoltzService;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IMempoolMonitoringService> _mockMempoolService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletService;

    public BoltzSwapTests()
    {
        _mockBoltzService = new Mock<IBoltzSwapService>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockMempoolService = new Mock<IMempoolMonitoringService>();
        _mockWalletService = new Mock<IWalletAccountBalanceService>();
    }

    [Fact]
    public async Task CreateSwap_WithValidAmount_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = "test-project";
        var amount = new Amount(100000);
        var address = "bc1qtest123";

        var pairInfo = new BoltzPairInfo
        {
            MinAmount = 10000,
            MaxAmount = 10000000,
            FeePercentage = 0.5m,
            MinerFee = 500
        };

        var expectedSwap = new BoltzSubmarineSwap
        {
            Id = "swap-123",
            Invoice = "lnbc1000n1...",
            Address = address,
            ExpectedAmount = 99000,
            Status = SwapState.Created
        };

        _mockBoltzService
            .Setup(x => x.GetPairInfoAsync())
            .ReturnsAsync(Result.Success(pairInfo));

        _mockBoltzService
            .Setup(x => x.CreateSubmarineSwapAsync(address, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedSwap));

        var logger = new Mock<ILogger<CreateLightningSwapForInvestment.CreateLightningSwapHandler>>();
        var handler = new CreateLightningSwapForInvestment.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockDerivationOperations.Object,
            logger.Object);

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("swap-123", result.Value.Swap.Id);
        Assert.Equal("lnbc1000n1...", result.Value.Swap.Invoice);
    }

    [Fact]
    public async Task CreateSwap_AmountTooSmall_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = "test-project";
        var amount = new Amount(1000); // Too small
        var address = "bc1qtest123";

        var pairInfo = new BoltzPairInfo
        {
            MinAmount = 10000,
            MaxAmount = 10000000,
            FeePercentage = 0.5m,
            MinerFee = 500
        };

        _mockBoltzService
            .Setup(x => x.GetPairInfoAsync())
            .ReturnsAsync(Result.Success(pairInfo));

        var logger = new Mock<ILogger<CreateLightningSwapForInvestment.CreateLightningSwapHandler>>();
        var handler = new CreateLightningSwapForInvestment.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockDerivationOperations.Object,
            logger.Object);

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("too small", result.Error.ToLower());
    }

    [Fact]
    public async Task MonitorSwap_WhenCompleted_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";
        var address = "bc1qtest123";
        var expectedAmount = 99000L;

        var completedStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.TransactionClaimed,
            TransactionId = "txid123"
        };

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                outpoint = new Outpoint { transactionId = "txid123", outputIndex = 0 },
                value = expectedAmount,
                address = address
            }
        };

        _mockBoltzService
            .Setup(x => x.GetSwapStatusAsync(swapId))
            .ReturnsAsync(Result.Success(completedStatus));

        _mockMempoolService
            .Setup(x => x.MonitorAddressForFundsAsync(address, expectedAmount, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(utxos);

        var logger = new Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>>();
        var handler = new MonitorLightningSwap.MonitorLightningSwapHandler(
            _mockBoltzService.Object,
            _mockMempoolService.Object,
            _mockWalletService.Object,
            logger.Object);

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            walletId, swapId, address, expectedAmount);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SwapState.TransactionClaimed, result.Value.SwapStatus.Status);
        Assert.Equal("txid123", result.Value.TransactionId);
        Assert.NotNull(result.Value.DetectedUtxos);
    }

    [Fact]
    public async Task MonitorSwap_WhenExpired_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-expired";
        var address = "bc1qtest123";

        var expiredStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.SwapExpired,
            FailureReason = "Invoice not paid in time"
        };

        _mockBoltzService
            .Setup(x => x.GetSwapStatusAsync(swapId))
            .ReturnsAsync(Result.Success(expiredStatus));

        var logger = new Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>>();
        var handler = new MonitorLightningSwap.MonitorLightningSwapHandler(
            _mockBoltzService.Object,
            _mockMempoolService.Object,
            _mockWalletService.Object,
            logger.Object);

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            walletId, swapId, address, 100000);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error.ToLower());
    }

    [Fact]
    public async Task GetPairInfo_ReturnsValidLimits()
    {
        // Arrange
        var expectedInfo = new BoltzPairInfo
        {
            PairId = "BTC/BTC",
            MinAmount = 10000,
            MaxAmount = 10000000,
            FeePercentage = 0.5m,
            MinerFee = 500
        };

        _mockBoltzService
            .Setup(x => x.GetPairInfoAsync())
            .ReturnsAsync(Result.Success(expectedInfo));

        // Act
        var result = await _mockBoltzService.Object.GetPairInfoAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10000, result.Value.MinAmount);
        Assert.Equal(10000000, result.Value.MaxAmount);
        Assert.Equal(0.5m, result.Value.FeePercentage);
    }

    [Fact]
    public void SwapState_IsComplete_ReturnsTrueForClaimed()
    {
        Assert.True(SwapState.TransactionClaimed.IsComplete());
        Assert.False(SwapState.InvoicePaid.IsComplete());
        Assert.False(SwapState.TransactionMempool.IsComplete());
    }

    [Fact]
    public void SwapState_IsFailed_ReturnsTrueForFailedStates()
    {
        Assert.True(SwapState.SwapExpired.IsFailed());
        Assert.True(SwapState.InvoiceExpired.IsFailed());
        Assert.True(SwapState.InvoiceFailedToPay.IsFailed());
        Assert.True(SwapState.TransactionRefunded.IsFailed());
        Assert.False(SwapState.InvoicePaid.IsFailed());
    }

    [Fact]
    public void SwapState_IsPending_ReturnsTrueForPendingStates()
    {
        Assert.True(SwapState.Created.IsPending());
        Assert.True(SwapState.InvoicePaid.IsPending());
        Assert.True(SwapState.TransactionMempool.IsPending());
        Assert.False(SwapState.TransactionClaimed.IsPending());
        Assert.False(SwapState.SwapExpired.IsPending());
    }
}


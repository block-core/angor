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
/// Unit tests for CreateLightningSwapForInvestment handler
/// </summary>
public class CreateLightningSwapTests
{
    private readonly Mock<IBoltzSwapService> _mockBoltzService;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ILogger<CreateLightningSwapForInvestment.CreateLightningSwapHandler>> _mockLogger;

    public CreateLightningSwapTests()
    {
        _mockBoltzService = new Mock<IBoltzSwapService>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockLogger = new Mock<ILogger<CreateLightningSwapForInvestment.CreateLightningSwapHandler>>();
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

        var handler = new CreateLightningSwapForInvestment.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockDerivationOperations.Object,
            _mockLogger.Object);

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

        var handler = new CreateLightningSwapForInvestment.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockDerivationOperations.Object,
            _mockLogger.Object);

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("too small", result.Error.ToLower());
    }

    [Fact]
    public async Task CreateSwap_AmountTooLarge_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var projectId = "test-project";
        var amount = new Amount(100000000); // Too large
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

        var handler = new CreateLightningSwapForInvestment.CreateLightningSwapHandler(
            _mockBoltzService.Object,
            _mockDerivationOperations.Object,
            _mockLogger.Object);

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            walletId, projectId, amount, address);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("too large", result.Error.ToLower());
    }
}

/// <summary>
/// Unit tests for MonitorLightningSwap handler
/// </summary>
public class MonitorLightningSwapTests
{
    private readonly Mock<IBoltzSwapService> _mockBoltzService;
    private readonly Mock<IIndexerService> _mockIndexerService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletService;
    private readonly Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>> _mockLogger;
    private readonly MonitorLightningSwap.MonitorLightningSwapHandler _handler;

    public MonitorLightningSwapTests()
    {
        _mockBoltzService = new Mock<IBoltzSwapService>();
        _mockIndexerService = new Mock<IIndexerService>();
        _mockWalletService = new Mock<IWalletAccountBalanceService>();
        _mockLogger = new Mock<ILogger<MonitorLightningSwap.MonitorLightningSwapHandler>>();

        _handler = new MonitorLightningSwap.MonitorLightningSwapHandler(
            _mockBoltzService.Object,
            _mockIndexerService.Object,
            _mockWalletService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenSwapCompleted_FetchesUtxosFromIndexer()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-123";
        var address = "bc1qtest123";

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
                value = 100000,
                address = address
            }
        };

        _mockBoltzService
            .Setup(x => x.GetSwapStatusAsync(swapId))
            .ReturnsAsync(Result.Success(completedStatus));

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(utxos);

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            walletId, swapId, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SwapState.TransactionClaimed, result.Value.SwapStatus.Status);
        Assert.Equal("txid123", result.Value.TransactionId);
        Assert.NotNull(result.Value.DetectedUtxos);
        Assert.Single(result.Value.DetectedUtxos);

        // Verify indexer was called once (not polling)
        _mockIndexerService.Verify(
            x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSwapInMempool_ReturnsSuccessWithTxId()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-mempool";
        var address = "bc1qtest456";

        var mempoolStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.TransactionMempool,
            TransactionId = "txid456"
        };

        _mockBoltzService
            .Setup(x => x.GetSwapStatusAsync(swapId))
            .ReturnsAsync(Result.Success(mempoolStatus));

        _mockIndexerService
            .Setup(x => x.FetchUtxoAsync(address, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<UtxoData>());

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            walletId, swapId, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SwapState.TransactionMempool, result.Value.SwapStatus.Status);
        Assert.Equal("txid456", result.Value.TransactionId);
    }

    [Fact]
    public async Task Handle_WhenSwapExpired_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-expired";
        var address = "bc1qtest789";

        var expiredStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.SwapExpired,
            FailureReason = "Invoice not paid in time"
        };

        _mockBoltzService
            .Setup(x => x.GetSwapStatusAsync(swapId))
            .ReturnsAsync(Result.Success(expiredStatus));

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            walletId, swapId, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error.ToLower());
    }

    [Fact]
    public async Task Handle_WhenInvoiceExpired_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var swapId = "swap-invoice-expired";
        var address = "bc1qtest000";

        var expiredStatus = new BoltzSwapStatus
        {
            SwapId = swapId,
            Status = SwapState.InvoiceExpired,
            FailureReason = "Invoice expired before payment"
        };

        _mockBoltzService
            .Setup(x => x.GetSwapStatusAsync(swapId))
            .ReturnsAsync(Result.Success(expiredStatus));

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            walletId, swapId, address);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("InvoiceExpired", result.Error);
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

/// <summary>
/// Unit tests for BoltzPairInfo
/// </summary>
public class BoltzPairInfoTests
{
    [Fact]
    public void FeeCalculation_ReturnsCorrectValues()
    {
        var pairInfo = new BoltzPairInfo
        {
            MinAmount = 10000,
            MaxAmount = 10000000,
            FeePercentage = 0.5m,
            MinerFee = 500
        };

        // Calculate expected fee for 100,000 sats
        long amount = 100000;
        var percentageFee = (long)(amount * pairInfo.FeePercentage / 100);
        var totalFee = percentageFee + pairInfo.MinerFee;
        var expectedReceived = amount - totalFee;

        Assert.Equal(500, percentageFee); // 0.5% of 100,000
        Assert.Equal(1000, totalFee);     // 500 + 500 miner fee
        Assert.Equal(99000, expectedReceived);
    }
}


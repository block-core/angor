using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Unit tests for CreateLightningInvoiceForInvestment handler
/// </summary>
public class CreateLightningInvoiceForInvestmentTests
{
    private readonly Mock<IBoltService> _mockBoltService;
    private readonly Mock<ILogger<CreateLightningInvoiceForInvestment.CreateLightningInvoiceHandler>> _mockLogger;
    private readonly CreateLightningInvoiceForInvestment.CreateLightningInvoiceHandler _handler;

    public CreateLightningInvoiceForInvestmentTests()
    {
        _mockBoltService = new Mock<IBoltService>();
        _mockLogger = new Mock<ILogger<CreateLightningInvoiceForInvestment.CreateLightningInvoiceHandler>>();
        _handler = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceHandler(
            _mockBoltService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenBoltWalletExists_CreatesInvoiceSuccessfully()
    {
        // Arrange
        var walletId = new WalletId("test-wallet-123");
        var projectId = "test-project-abc";
        var amount = new Amount(100000);

        var existingWallet = new BoltWallet
        {
            Id = walletId.Value,
            UserId = walletId.Value,
            BalanceSats = 0,
            CreatedAt = DateTime.UtcNow
        };

        var expectedInvoice = new BoltInvoice
        {
            Id = "invoice-123",
            WalletId = walletId.Value,
            Bolt11 = "lnbc1000n1...",
            PaymentHash = "hash123",
            AmountSats = amount.Sats,
            Status = BoltPaymentStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _mockBoltService
            .Setup(x => x.GetWalletAsync(walletId.Value))
            .ReturnsAsync(Result.Success(existingWallet));

        _mockBoltService
            .Setup(x => x.CreateInvoiceAsync(walletId.Value, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedInvoice));

        var request = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
            walletId, projectId, amount);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedInvoice.Id, result.Value.Invoice.Id);
        Assert.Equal(expectedInvoice.Bolt11, result.Value.Invoice.Bolt11);
        Assert.Equal(walletId.Value, result.Value.BoltWalletId);
    }

    [Fact]
    public async Task Handle_WhenBoltWalletDoesNotExist_CreatesWalletAndInvoice()
    {
        // Arrange
        var walletId = new WalletId("new-wallet-456");
        var projectId = "test-project-xyz";
        var amount = new Amount(50000);

        var newWallet = new BoltWallet
        {
            Id = walletId.Value,
            UserId = walletId.Value,
            BalanceSats = 0,
            CreatedAt = DateTime.UtcNow
        };

        var expectedInvoice = new BoltInvoice
        {
            Id = "invoice-456",
            WalletId = walletId.Value,
            Bolt11 = "lnbc500n1...",
            PaymentHash = "hash456",
            AmountSats = amount.Sats,
            Status = BoltPaymentStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _mockBoltService
            .Setup(x => x.GetWalletAsync(walletId.Value))
            .ReturnsAsync(Result.Failure<BoltWallet>("Wallet not found"));

        _mockBoltService
            .Setup(x => x.CreateWalletAsync(walletId.Value))
            .ReturnsAsync(Result.Success(newWallet));

        _mockBoltService
            .Setup(x => x.CreateInvoiceAsync(walletId.Value, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(expectedInvoice));

        var request = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
            walletId, projectId, amount);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedInvoice.Id, result.Value.Invoice.Id);
        _mockBoltService.Verify(x => x.CreateWalletAsync(walletId.Value), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvoiceCreationFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet-789");
        var projectId = "test-project-def";
        var amount = new Amount(25000);

        var existingWallet = new BoltWallet { Id = walletId.Value };

        _mockBoltService
            .Setup(x => x.GetWalletAsync(walletId.Value))
            .ReturnsAsync(Result.Success(existingWallet));

        _mockBoltService
            .Setup(x => x.CreateInvoiceAsync(walletId.Value, amount.Sats, It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<BoltInvoice>("API error"));

        var request = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
            walletId, projectId, amount);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("API error", result.Error);
    }

    [Fact]
    public async Task Handle_WithCustomMemo_UsesProvidedMemo()
    {
        // Arrange
        var walletId = new WalletId("test-wallet-memo");
        var projectId = "test-project-ghi";
        var amount = new Amount(75000);
        var customMemo = "Custom investment memo";

        var wallet = new BoltWallet { Id = walletId.Value };
        var invoice = new BoltInvoice
        {
            Id = "invoice-memo",
            Bolt11 = "lnbc750n1...",
            AmountSats = amount.Sats,
            Memo = customMemo
        };

        _mockBoltService
            .Setup(x => x.GetWalletAsync(walletId.Value))
            .ReturnsAsync(Result.Success(wallet));

        _mockBoltService
            .Setup(x => x.CreateInvoiceAsync(walletId.Value, amount.Sats, customMemo))
            .ReturnsAsync(Result.Success(invoice));

        var request = new CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest(
            walletId, projectId, amount, customMemo);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockBoltService.Verify(
            x => x.CreateInvoiceAsync(walletId.Value, amount.Sats, customMemo),
            Times.Once);
    }
}

/// <summary>
/// Unit tests for MonitorLightningInvoiceAndSwap handler
/// </summary>
public class MonitorLightningInvoiceAndSwapTests
{
    private readonly Mock<IBoltService> _mockBoltService;
    private readonly Mock<IMempoolMonitoringService> _mockMempoolService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletService;
    private readonly Mock<ILogger<MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceHandler>> _mockLogger;
    private readonly MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceHandler _handler;

    public MonitorLightningInvoiceAndSwapTests()
    {
        _mockBoltService = new Mock<IBoltService>();
        _mockMempoolService = new Mock<IMempoolMonitoringService>();
        _mockWalletService = new Mock<IWalletAccountBalanceService>();
        _mockLogger = new Mock<ILogger<MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceHandler>>();
        
        _handler = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceHandler(
            _mockBoltService.Object,
            _mockMempoolService.Object,
            _mockWalletService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenInvoicePaidAndSwapCompletes_ReturnsSuccess()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var invoiceId = "invoice-123";
        var boltWalletId = "bolt-wallet-123";
        var targetAddress = "bc1qtest123";
        var swapAddress = "bc1qswap456";

        var paidInvoice = new BoltInvoice
        {
            Id = invoiceId,
            Status = BoltPaymentStatus.Paid,
            AmountSats = 100000,
            PaidAt = DateTime.UtcNow
        };

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                outpoint = new Outpoint { transactionId = "tx1", outputIndex = 0 },
                value = 100000,
                address = swapAddress
            }
        };

        _mockBoltService
            .Setup(x => x.GetInvoiceAsync(invoiceId))
            .ReturnsAsync(Result.Success(paidInvoice));

        _mockBoltService
            .Setup(x => x.GetSwapAddressAsync(boltWalletId, 100000))
            .ReturnsAsync(Result.Success(swapAddress));

        _mockMempoolService
            .Setup(x => x.MonitorAddressForFundsAsync(
                swapAddress,
                100000,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(utxos);

        var request = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest(
            walletId, invoiceId, boltWalletId, targetAddress);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(swapAddress, result.Value.SwapAddress);
        Assert.Equal(paidInvoice.Id, result.Value.Invoice.Id);
        Assert.NotNull(result.Value.DetectedUtxos);
        Assert.Single(result.Value.DetectedUtxos);
    }

    [Fact]
    public async Task Handle_WhenInvoiceExpires_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var invoiceId = "invoice-expired";
        var boltWalletId = "bolt-wallet";
        var targetAddress = "bc1qtest";

        var expiredInvoice = new BoltInvoice
        {
            Id = invoiceId,
            Status = BoltPaymentStatus.Expired,
            AmountSats = 50000
        };

        _mockBoltService
            .Setup(x => x.GetInvoiceAsync(invoiceId))
            .ReturnsAsync(Result.Success(expiredInvoice));

        var request = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest(
            walletId, invoiceId, boltWalletId, targetAddress);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error.ToLower());
    }

    [Fact]
    public async Task Handle_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("test-wallet");
        var invoiceId = "invoice-cancel";
        var boltWalletId = "bolt-wallet";
        var targetAddress = "bc1qtest";

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var request = new MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest(
            walletId, invoiceId, boltWalletId, targetAddress);

        // Act
        var result = await _handler.Handle(request, cancellationTokenSource.Token);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("cancelled", result.Error.ToLower());
    }
}

/// <summary>
/// Unit tests for BoltService
/// </summary>
public class BoltServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly BoltConfiguration _configuration;
    private readonly Mock<ILogger<BoltService>> _mockLogger;

    public BoltServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _configuration = new BoltConfiguration
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.test.bolt",
            TimeoutSeconds = 30
        };
        _mockLogger = new Mock<ILogger<BoltService>>();
    }

    [Fact]
    public void Constructor_SetsUpHttpClientCorrectly()
    {
        // Act
        var service = new BoltService(_httpClient, _configuration, _mockLogger.Object);

        // Assert
        Assert.Equal(new Uri(_configuration.BaseUrl), _httpClient.BaseAddress);
        Assert.Contains(_httpClient.DefaultRequestHeaders, 
            h => h.Key == "Authorization" && h.Value.Contains("Bearer test-api-key"));
    }

    [Fact]
    public void Constructor_ThrowsWhenHttpClientIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new BoltService(null!, _configuration, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenConfigurationIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new BoltService(_httpClient, null!, _mockLogger.Object));
    }
}


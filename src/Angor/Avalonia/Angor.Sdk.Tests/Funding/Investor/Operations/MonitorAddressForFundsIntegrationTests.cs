using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Tests.Funding.TestDoubles;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Integration tests for MonitorAddressForFunds handler.
/// These tests actually monitor the mempool on Angornet (Bitcoin Signet) and verify funds detection works.
/// 
/// IMPORTANT: These tests require:
/// 1. Access to Angornet (Bitcoin Signet) blockchain
/// 2. Angor indexer service (https://signet.angor.online)
/// 3. Real signet bitcoins (small amounts from miner faucet)
/// 
/// Run these tests manually when you have Angornet setup ready.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Network", "Angornet")]
public class MonitorAddressForFundsIntegrationTests : IDisposable
{
    private readonly Network _network;
    private readonly NetworkConfiguration _networkConfiguration;
    private readonly WalletOperations _walletOperations;
    private readonly DerivationOperations _derivationOperations;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly IIndexerService _realIndexerService;
    private readonly IMempoolMonitoringService _realMempoolMonitoringService;
    private readonly MonitorAddressForFunds.MonitorAddressForFundsHandler _sut;
    private readonly ITestOutputHelper _output;

    // Test wallet words - ONLY FOR ANGORNET (SIGNET)!
    private const string TestWalletWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestWalletPassphrase = "";

    public MonitorAddressForFundsIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Setup network - Use Angornet (Bitcoin Signet)
        _networkConfiguration = new NetworkConfiguration();
        _networkConfiguration.SetNetwork(new Angornet());
        _network = _networkConfiguration.GetNetwork();

        // Create derivation operations first (needed by indexer)
        _derivationOperations = new DerivationOperations(
            new HdOperations(),
            new NullLogger<DerivationOperations>(),
            _networkConfiguration);

        // Setup REAL indexer service using MempoolSpaceIndexerApi for Angornet
        var httpClientFactory = new TestHttpClientFactory("https://signet.angor.online");

        var networkService = new TestNetworkService(
            new SettingsUrl { Name = "Angornet", Url = "https://signet.angor.online" },
            _networkConfiguration);

        _realIndexerService = new MempoolSpaceIndexerApi(
            new NullLogger<MempoolSpaceIndexerApi>(),
            httpClientFactory,
            networkService,
            _derivationOperations);

        // Setup REAL mempool monitoring service
        _realMempoolMonitoringService = new MempoolMonitoringService(
            _realIndexerService,
            new NullLogger<MempoolMonitoringService>());

        // Setup wallet operations with real indexer
        _walletOperations = new WalletOperations(
            _realIndexerService,
            new HdOperations(),
            new NullLogger<WalletOperations>(),
            _networkConfiguration);

        // Setup mocks for non-critical services
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();

        // Create the handler with REAL mempool monitoring service
        _sut = new MonitorAddressForFunds.MonitorAddressForFundsHandler(
            _realMempoolMonitoringService,
            _mockWalletAccountBalanceService.Object,
            new NullLogger<MonitorAddressForFunds.MonitorAddressForFundsHandler>());
    }

    [Fact]
    public async Task MonitorAddressForFunds_WhenFundsSentToAddress_DetectsFundsSuccessfully()
    {
        // Arrange
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);

        // Generate a fresh address to monitor - use a unique index to avoid UTXO conflicts
        var uniqueIndex = (int)(DateTime.UtcNow.Ticks % 1000);
        for (int i = 0; i < uniqueIndex; i++)
        {
            accountInfo.LastFetchIndex++;
        }
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var addressToMonitor = accountInfo.AddressesInfo.Last();

        var walletId = new WalletId(accountInfo.walletId);
        var requiredAmount = new Amount(10000000); // 0.1 BTC

        _output.WriteLine(new string('=', 80));
        _output.WriteLine("🔍 INTEGRATION TEST: MonitorAddressForFunds");
        _output.WriteLine(new string('=', 80));
        _output.WriteLine($"Address to monitor: {addressToMonitor.Address}");
        _output.WriteLine($"Required amount: {requiredAmount.Sats} sats (0.1 BTC)");
        _output.WriteLine(new string('-', 80));

        // Setup account balance mock with the address
        var accountBalance = new AccountBalanceInfo();
        accountBalance.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalance));

        // Create the monitoring request with a timeout
        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            addressToMonitor.Address,
            requiredAmount,
            TimeSpan.FromMinutes(2)); // 2 minute timeout for the test

        // Create miner faucet to fund the address
        var minerFaucet = new AngornetMinerFaucet(
            _walletOperations,
            _realIndexerService,
            _network,
            new NullLogger<AngornetMinerFaucet>());

        // Start monitoring BEFORE sending funds - this is the key test scenario
        _output.WriteLine("\n📡 Starting address monitoring...");
        _output.WriteLine($"   Timeout: 2 minutes");

        var monitoringTask = _sut.Handle(request, CancellationToken.None);

        // Wait a moment to ensure monitoring has started
        _output.WriteLine("   Waiting 2 seconds for monitoring to start...");
        await Task.Delay(2000);

        // Now send funds to the monitored address
        _output.WriteLine("\n💰 Sending funds to monitored address using miner faucet...");
        var fundingAmount = 15000000L; // 0.15 BTC (more than required)
        
        var fundingTxId = await minerFaucet.FundAddressAsync(
            addressToMonitor.Address,
            fundingAmount,
            10); // 10 sat/vB fee

        _output.WriteLine($"✅ Funding transaction published: {fundingTxId}");
        _output.WriteLine($"   Explorer: https://signet.angor.online/tx/{fundingTxId}");
        _output.WriteLine($"   Amount sent: {fundingAmount} sats");

        // Wait for the monitoring task to complete
        _output.WriteLine("\n⏳ Waiting for monitoring to detect the funds...");
        var result = await monitoringTask;

        // Assert
        var error = result.IsFailure ? result.Error : string.Empty;

        Assert.True(result.IsSuccess, $"MonitorAddressForFunds failed: {error}");
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.DetectedUtxos);
        Assert.True(result.Value.TotalAmount.Sats >= requiredAmount.Sats,
            $"Expected at least {requiredAmount.Sats} sats, but got {result.Value.TotalAmount.Sats}");
        Assert.Equal(addressToMonitor.Address, result.Value.Address);

        // Output results
        _output.WriteLine("\n" + new string('=', 80));
        _output.WriteLine("✅ TEST PASSED - Funds detected successfully!");
        _output.WriteLine(new string('=', 80));
        _output.WriteLine($"Detected UTXOs: {result.Value.DetectedUtxos.Count}");
        _output.WriteLine($"Total amount detected: {result.Value.TotalAmount.Sats} sats");
        _output.WriteLine($"Address monitored: {result.Value.Address}");
        
        foreach (var utxo in result.Value.DetectedUtxos)
        {
            _output.WriteLine($"  - UTXO: {utxo.outpoint}, Value: {utxo.value} sats");
        }
    }

    [Fact]
    public async Task MonitorAddressForFunds_WhenAddressNotInWallet_ReturnsFailure()
    {
        // Arrange
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        var walletId = new WalletId(accountInfo.walletId);

        // Setup account balance mock with empty addresses
        var accountBalance = new AccountBalanceInfo();
        accountBalance.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalance));

        // Use an address that is NOT in the wallet
        var invalidAddress = "tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx";

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            invalidAddress,
            new Amount(10000000),
            TimeSpan.FromSeconds(5));

        _output.WriteLine(new string('=', 80));
        _output.WriteLine("🔍 TEST: Address not in wallet validation");
        _output.WriteLine(new string('=', 80));
        _output.WriteLine($"Testing with address: {invalidAddress}");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("not found in wallet", result.Error);

        _output.WriteLine($"✅ Expected failure received: {result.Error}");
    }

    [Fact]
    public async Task MonitorAddressForFunds_WhenCancelled_ReturnsFailure()
    {
        // Arrange
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var addressToMonitor = accountInfo.AddressesInfo.First();
        var walletId = new WalletId(accountInfo.walletId);

        // Setup account balance mock
        var accountBalance = new AccountBalanceInfo();
        accountBalance.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalance));

        var request = new MonitorAddressForFunds.MonitorAddressForFundsRequest(
            walletId,
            addressToMonitor.Address,
            new Amount(10000000),
            TimeSpan.FromMinutes(5)); // Long timeout

        _output.WriteLine(new string('=', 80));
        _output.WriteLine("🔍 TEST: Cancellation handling");
        _output.WriteLine(new string('=', 80));
        _output.WriteLine($"Address: {addressToMonitor.Address}");

        // Create cancellation token and cancel after 2 seconds
        using var cts = new CancellationTokenSource();
        
        var monitoringTask = _sut.Handle(request, cts.Token);
        
        _output.WriteLine("Waiting 2 seconds then cancelling...");
        await Task.Delay(2000);
        cts.Cancel();

        // Act
        var result = await monitoringTask;

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("cancelled", result.Error.ToLower());

        _output.WriteLine($"✅ Expected cancellation result: {result.Error}");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}


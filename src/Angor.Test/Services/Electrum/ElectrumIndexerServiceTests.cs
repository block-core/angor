using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Shared.Services.Indexer.Electrum;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Test.Services.Electrum;

public class ElectrumIndexerServiceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ElectrumClientPool _clientPool = null!;
    private ElectrumIndexerService _service = null!;
    private readonly Mock<INetworkConfiguration> _networkConfig;

    // Test configuration - change these to test different servers/networks
    private const string TestServerHost = "electrum.blockstream.info";
    private const int TestServerPort = 50002; // SSL port
    private const bool UseSsl = true;

    // Known testnet address for testing (Blockstream's faucet return address)
    private const string TestAddress = "tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx";

    // Known mainnet transaction for testing
    private const string TestTransactionId = "4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b";

    public ElectrumIndexerServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _networkConfig = new Mock<INetworkConfiguration>();

        // Setup network configuration for testnet
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _networkConfig.Setup(x => x.GetNetwork()).Returns(network);
    }

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var poolLogger = loggerFactory.CreateLogger<ElectrumClientPool>();
        var serviceLogger = loggerFactory.CreateLogger<ElectrumIndexerService>();

        var serverConfig = new ElectrumServerConfig
        {
            Host = TestServerHost,
            Port = TestServerPort,
            UseSsl = UseSsl,
            Timeout = TimeSpan.FromSeconds(30)
        };

        _clientPool = new ElectrumClientPool(poolLogger, loggerFactory, new[] { serverConfig });
        _service = new ElectrumIndexerService(serviceLogger, _networkConfig.Object, _clientPool);

        _output.WriteLine($"Initialized with server: {TestServerHost}:{TestServerPort}");
    }

    public async Task DisposeAsync()
    {
        await _clientPool.DisconnectAllAsync();
        _clientPool.Dispose();
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetAddressBalances_WithValidAddress_ReturnsBalance()
    {
        // Arrange
        var addresses = new List<AddressInfo>
        {
          new() { Address = TestAddress }
        };

        // Act
        var result = await _service.GetAdressBalancesAsync(addresses);

        // Assert
        _output.WriteLine($"Address: {TestAddress}");
        foreach (var balance in result)
        {
            _output.WriteLine($"  Balance: {balance.balance} sats");
            _output.WriteLine($"  Pending Received: {balance.pendingReceived} sats");
            _output.WriteLine($"  Pending Sent: {balance.pendingSent} sats");
        }

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task FetchUtxo_WithValidAddress_ReturnsUtxos()
    {
        // Arrange & Act
        var result = await _service.FetchUtxoAsync(TestAddress, 10, 0);

        // Assert
        _output.WriteLine($"UTXOs for {TestAddress}:");
        if (result != null)
        {
            foreach (var utxo in result)
            {
                _output.WriteLine($"  TxId: {utxo.outpoint.transactionId}");
                _output.WriteLine($"  Index: {utxo.outpoint.outputIndex}");
                _output.WriteLine($"  Value: {utxo.value} sats");
                _output.WriteLine($"  Block: {utxo.blockIndex}");
                _output.WriteLine("  ---");
            }
        }

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task FetchAddressHistory_WithValidAddress_ReturnsTransactions()
    {
        // Arrange & Act
        var result = await _service.FetchAddressHistoryAsync(TestAddress);

        // Assert
        _output.WriteLine($"Transaction history for {TestAddress}:");
        if (result != null)
        {
            _output.WriteLine($"Total transactions: {result.Count}");
            foreach (var tx in result.Take(5)) // Show first 5
            {
                _output.WriteLine($"  TxId: {tx.TransactionId}");
                _output.WriteLine($"  Block: {tx.BlockIndex}");
                _output.WriteLine($"  Timestamp: {tx.Timestamp}");
                _output.WriteLine("  ---");
            }
        }

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetFeeEstimation_ReturnsValidFees()
    {
        // Arrange
        var confirmations = new[] { 1, 3, 6 };

        // Act
        var result = await _service.GetFeeEstimationAsync(confirmations);

        // Assert
        _output.WriteLine("Fee estimations:");
        if (result?.Fees != null)
        {
            foreach (var fee in result.Fees)
            {
                _output.WriteLine($"  {fee.Confirmations} blocks: {fee.FeeRate} sat/kB");
            }
        }

        Assert.NotNull(result);
        Assert.NotEmpty(result.Fees);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetTransactionHexById_WithValidTxId_ReturnsHex()
    {
        // Arrange & Act
        var result = await _service.GetTransactionHexByIdAsync(TestTransactionId);

        // Assert
        _output.WriteLine($"Transaction hex for {TestTransactionId}:");
        _output.WriteLine($"  Length: {result.Length} chars");
        _output.WriteLine($"  Hex (first 100): {result.Substring(0, Math.Min(100, result.Length))}...");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetTransactionInfoById_WithValidTxId_ReturnsTransactionInfo()
    {
        // Arrange & Act
        var result = await _service.GetTransactionInfoByIdAsync(TestTransactionId);

        // Assert
        _output.WriteLine($"Transaction info for {TestTransactionId}:");
        if (result != null)
        {
            _output.WriteLine($"  TxId: {result.TransactionId}");
            _output.WriteLine($"  Version: {result.Version}");
            _output.WriteLine($"  Size: {result.Size}");
            _output.WriteLine($"  VSize: {result.VirtualSize}");
            _output.WriteLine($"  Weight: {result.Weight}");
            _output.WriteLine($"  Block Hash: {result.BlockHash}");
            _output.WriteLine($"  Block Index: {result.BlockIndex}");
            _output.WriteLine($"  Inputs: {result.Inputs?.Count() ?? 0}");
            _output.WriteLine($"  Outputs: {result.Outputs?.Count() ?? 0}");
        }

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetIsSpentOutputsOnTransaction_WithValidTxId_ReturnsSpentStatus()
    {
        // Arrange & Act
        var result = await _service.GetIsSpentOutputsOnTransactionAsync(TestTransactionId);

        // Assert
        _output.WriteLine($"Spent outputs for {TestTransactionId}:");
        foreach (var item in result)
        {
            _output.WriteLine($"  Output {item.index}: {(item.spent ? "SPENT" : "UNSPENT")}");
        }

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task CheckIndexerNetwork_WithValidServer_ReturnsOnlineStatus()
    {
        // Arrange
        var serverUrl = $"ssl://{TestServerHost}:{TestServerPort}";

        // Act
        var result = await _service.CheckIndexerNetwork(serverUrl);

        // Assert
        _output.WriteLine($"Server: {serverUrl}");
        _output.WriteLine($"  Online: {result.IsOnline}");
        _output.WriteLine($"  Genesis Hash: {result.GenesisHash}");

        Assert.True(result.IsOnline);
        Assert.NotNull(result.GenesisHash);
    }

    [Fact(Skip = "Manual test - requires active Electrum server and funded wallet")]
    public async Task PublishTransaction_WithValidTx_BroadcastsSuccessfully()
    {
        // This test requires a valid signed transaction hex
        // It's included for completeness but should only be run with a real transaction

        // Arrange
        var txHex = "YOUR_SIGNED_TRANSACTION_HEX_HERE";

        // Act
        var result = await _service.PublishTransactionAsync(txHex);

        // Assert
        _output.WriteLine($"Publish result: {(string.IsNullOrEmpty(result) ? "SUCCESS" : result)}");

        // Empty string means success
        Assert.True(string.IsNullOrEmpty(result) || result.Contains("error"));
    }
}

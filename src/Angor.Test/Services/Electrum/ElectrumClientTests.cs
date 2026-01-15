using Angor.Shared.Services.Indexer.Electrum;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Test.Services.Electrum;

/// <summary>
/// Manual integration tests for ElectrumClient.
/// These tests connect to real Electrum servers and should be run manually.
/// </summary>
public class ElectrumClientTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ElectrumClient _client = null!;
    private readonly ILogger<ElectrumClient> _logger;

    // Test configuration
    private const string TestServerHost = "electrum.blockstream.info";
    private const int TestServerPort = 50002;
    private const bool UseSsl = true;

    public ElectrumClientTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ElectrumClient>();
    }

    public Task InitializeAsync()
    {
        var config = new ElectrumServerConfig
        {
            Host = TestServerHost,
            Port = TestServerPort,
            UseSsl = UseSsl,
            AllowSelfSignedCertificates = true,
            Timeout = TimeSpan.FromSeconds(30)
        };

        _client = new ElectrumClient(_logger, config);
        _output.WriteLine($"Created client for {TestServerHost}:{TestServerPort}");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _client.DisconnectAsync();
        _client.Dispose();
    }

    [Fact]//(Skip = "Manual test - requires active Electrum server")]
    public async Task ConnectAsync_ToValidServer_Connects()
    {
        // Act
        await _client.ConnectAsync();

        // Assert
        _output.WriteLine($"Connected: {_client.IsConnected}");
        _output.WriteLine($"Server URL: {_client.ServerUrl}");

        Assert.True(_client.IsConnected);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task ServerVersion_ReturnsVersionInfo()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        var result = await _client.SendRequestAsync<string[]>(
              "server.version",
              new object[] { "AngorTest/1.0", "1.4" });

        // Assert
        _output.WriteLine("Server version info:");
        _output.WriteLine($"  Server: {result[0]}");
        _output.WriteLine($"  Protocol: {result[1]}");

        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task ServerBanner_ReturnsBanner()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        var result = await _client.SendRequestAsync<string>("server.banner");

        // Assert
        _output.WriteLine("Server banner:");
        _output.WriteLine(result);

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task ServerFeatures_ReturnsFeatures()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        var result = await _client.SendRequestAsync<System.Text.Json.JsonElement>("server.features");

        // Assert
        _output.WriteLine("Server features:");
        _output.WriteLine(result.ToString());

        Assert.NotNull(result.ToString());
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainHeadersSubscribe_ReturnsCurrentHeader()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act
        var result = await _client.SendRequestAsync<System.Text.Json.JsonElement>("blockchain.headers.subscribe");

        // Assert
        _output.WriteLine("Current block header:");
        _output.WriteLine(result.ToString());

        Assert.NotNull(result.ToString());
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainBlockHeader_ReturnsHeader()
    {
        // Arrange
        await _client.ConnectAsync();
        const int blockHeight = 0; // Genesis block

        // Act
        var result = await _client.SendRequestAsync<string>(
            "blockchain.block.header",
            new object[] { blockHeight });

        // Assert
        _output.WriteLine($"Block header at height {blockHeight}:");
        _output.WriteLine($"  Length: {result.Length} chars");
        _output.WriteLine($"  Header: {result.Substring(0, Math.Min(80, result.Length))}...");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainEstimateFee_ReturnsFeeRate()
    {
        // Arrange
        await _client.ConnectAsync();
        const int targetBlocks = 6;

        // Act
        var result = await _client.SendRequestAsync<double>(
            "blockchain.estimatefee",
 new object[] { targetBlocks });

        // Assert
        _output.WriteLine($"Fee estimate for {targetBlocks} blocks:");
        _output.WriteLine($"  BTC/kB: {result}");
        _output.WriteLine($"  sat/kB: {(long)(result * 100_000_000)}");

        // Fee can be -1 if not enough data
        Assert.True(result != 0);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainScripthashGetBalance_ReturnsBalance()
    {
        // Arrange
        await _client.ConnectAsync();

        // Known testnet address scripthash (you can calculate this using ElectrumScriptHashUtility)
        // This is a placeholder - replace with actual scripthash
        const string scriptHash = "8b01df4e368ea28f8dc0423bcf7a4923e3a12d307c875e47a0cfbf90b5c39161";

        // Act
        var result = await _client.SendRequestAsync<System.Text.Json.JsonElement>(
        "blockchain.scripthash.get_balance",
      new object[] { scriptHash });

        // Assert
        _output.WriteLine($"Balance for scripthash {scriptHash}:");
        _output.WriteLine(result.ToString());

        Assert.NotNull(result.ToString());
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainScripthashGetHistory_ReturnsHistory()
    {
        // Arrange
        await _client.ConnectAsync();
        const string scriptHash = "8b01df4e368ea28f8dc0423bcf7a4923e3a12d307c875e47a0cfbf90b5c39161";

        // Act
        var result = await _client.SendRequestAsync<System.Text.Json.JsonElement>(
        "blockchain.scripthash.get_history",
    new object[] { scriptHash });

        // Assert
        _output.WriteLine($"History for scripthash {scriptHash}:");
        _output.WriteLine(result.ToString());

        Assert.NotNull(result.ToString());
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainScripthashListUnspent_ReturnsUtxos()
    {
        // Arrange
        await _client.ConnectAsync();
        const string scriptHash = "8b01df4e368ea28f8dc0423bcf7a4923e3a12d307c875e47a0cfbf90b5c39161";

        // Act
        var result = await _client.SendRequestAsync<System.Text.Json.JsonElement>(
       "blockchain.scripthash.listunspent",
        new object[] { scriptHash });

        // Assert
        _output.WriteLine($"UTXOs for scripthash {scriptHash}:");
        _output.WriteLine(result.ToString());

        Assert.NotNull(result.ToString());
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainTransactionGet_ReturnsTransaction()
    {
        // Arrange
        await _client.ConnectAsync();

        // Bitcoin genesis coinbase transaction
        const string txId = "4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b";

        // Act - get raw hex
        var hexResult = await _client.SendRequestAsync<string>(
              "blockchain.transaction.get",
            new object[] { txId, false });

        // Assert
        _output.WriteLine($"Transaction hex for {txId}:");
        _output.WriteLine($"  Length: {hexResult.Length}");
        _output.WriteLine($"  Hex: {hexResult.Substring(0, Math.Min(100, hexResult.Length))}...");

        Assert.NotNull(hexResult);
        Assert.NotEmpty(hexResult);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task BlockchainTransactionGetVerbose_ReturnsTransactionDetails()
    {
        // Arrange
        await _client.ConnectAsync();
        const string txId = "4a5e1e4baab89f3a32518a88c31bc87f618f76673e2cc77ab2127b7afdeda33b";

        // Act - get verbose (decoded)
        var verboseResult = await _client.SendRequestAsync<System.Text.Json.JsonElement>(
               "blockchain.transaction.get",
               new object[] { txId, true });

        // Assert
        _output.WriteLine($"Transaction details for {txId}:");
        _output.WriteLine(verboseResult.ToString());

        Assert.NotNull(verboseResult.ToString());
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task MultipleRequests_HandlesConcurrently()
    {
        // Arrange
        await _client.ConnectAsync();

        // Act - send multiple requests
        var tasks = new List<Task<string>>
        {
            _client.SendRequestAsync<string>("server.banner"),
            _client.SendRequestAsync<string>("blockchain.block.header", new object[] { 0 }),
            _client.SendRequestAsync<string>("blockchain.block.header", new object[] { 1 }),
            _client.SendRequestAsync<string>("blockchain.block.header", new object[] { 2 })
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        _output.WriteLine($"Completed {results.Length} concurrent requests");
        for (int i = 0; i < results.Length; i++)
        {
            _output.WriteLine($"  Result {i}: {results[i].Substring(0, Math.Min(50, results[i].Length))}...");
        }

        Assert.Equal(4, results.Length);
        Assert.All(results, r => Assert.NotEmpty(r));
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task DisconnectAndReconnect_Works()
    {
        // Arrange
        await _client.ConnectAsync();
        Assert.True(_client.IsConnected);

        // Act - disconnect
        await _client.DisconnectAsync();
        Assert.False(_client.IsConnected);

        // Act - reconnect
        await _client.ConnectAsync();

        // Assert
        Assert.True(_client.IsConnected);
        _output.WriteLine("Successfully disconnected and reconnected");
    }
}

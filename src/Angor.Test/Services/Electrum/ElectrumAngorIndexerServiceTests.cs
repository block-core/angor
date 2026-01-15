using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Shared.Services.Indexer;
using Angor.Shared.Services.Indexer.Electrum;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Test.Services.Electrum;

public class ElectrumAngorIndexerServiceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ElectrumClientPool _clientPool = null!;
    private ElectrumAngorIndexerService _service = null!;
    private readonly Mock<INetworkConfiguration> _networkConfig;
    private readonly Mock<IDerivationOperations> _derivationOperations;

    private const string TestServerHost = "electrum.blockstream.info";
    private const int TestServerPort = 50002;
    private const bool UseSsl = true;

    private const string TestProjectId = "02a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";
    private const string TestProjectAddress = "tb1qexampleaddresshere";
    private const string TestInvestorPubKey = "03a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";

    public ElectrumAngorIndexerServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _networkConfig = new Mock<INetworkConfiguration>();
        _derivationOperations = new Mock<IDerivationOperations>();

        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _networkConfig.Setup(x => x.GetNetwork()).Returns(network);

        _derivationOperations
                  .Setup(x => x.ConvertAngorKeyToBitcoinAddress(It.IsAny<string>()))
              .Returns(TestProjectAddress);
    }

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var poolLogger = loggerFactory.CreateLogger<ElectrumClientPool>();
        var serviceLogger = loggerFactory.CreateLogger<ElectrumAngorIndexerService>();
        var mapperLogger = loggerFactory.CreateLogger<MempoolIndexerMappers>();

        var serverConfig = new ElectrumServerConfig
        {
            Host = TestServerHost,
            Port = TestServerPort,
            UseSsl = UseSsl,
            Timeout = TimeSpan.FromSeconds(30)
        };

        _clientPool = new ElectrumClientPool(poolLogger, loggerFactory, new[] { serverConfig });

        var mappers = new MempoolIndexerMappers(mapperLogger);

        _service = new ElectrumAngorIndexerService(
      serviceLogger,
       _networkConfig.Object,
     _derivationOperations.Object,
     _clientPool,
 mappers);

        _output.WriteLine($"Initialized with server: {TestServerHost}:{TestServerPort}");
    }

    public async Task DisposeAsync()
    {
        await _clientPool.DisconnectAllAsync();
        _clientPool.Dispose();
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetProjectsAsync_ReturnsEmptyList_NotSupportedViaElectrum()
    {
        var result = await _service.GetProjectsAsync(null, 10);

        _output.WriteLine("GetProjectsAsync is not supported via Electrum protocol");
        _output.WriteLine($"Result count: {result.Count}");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server and valid project")]
    public async Task GetProjectByIdAsync_WithValidProjectId_ReturnsProjectData()
    {
        _derivationOperations
         .Setup(x => x.ConvertAngorKeyToBitcoinAddress(TestProjectId))
             .Returns(TestProjectAddress);

        var result = await _service.GetProjectByIdAsync(TestProjectId);

        _output.WriteLine($"Project data for {TestProjectId}:");
        if (result != null)
        {
            _output.WriteLine($"  Project Identifier: {result.ProjectIdentifier}");
            _output.WriteLine($"  Founder Key: {result.FounderKey}");
            _output.WriteLine($"  Created On Block: {result.CreatedOnBlock}");
            _output.WriteLine($"  Nostr Event ID: {result.NostrEventId}");
            _output.WriteLine($"  Transaction ID: {result.TrxId}");
            _output.WriteLine($"  Total Investments Count: {result.TotalInvestmentsCount}");
        }
        else
        {
            _output.WriteLine("  No project data found (this may be expected if project doesn't exist)");
        }

        Assert.True(result == null || !string.IsNullOrEmpty(result.ProjectIdentifier));
    }

    [Fact(Skip = "Manual test - requires active Electrum server and valid project")]
    public async Task GetProjectStatsAsync_WithValidProjectId_ReturnsStats()
    {
        _derivationOperations
            .Setup(x => x.ConvertAngorKeyToBitcoinAddress(TestProjectId))
   .Returns(TestProjectAddress);

        var result = await _service.GetProjectStatsAsync(TestProjectId);

        _output.WriteLine($"Project stats for {result.projectId}:");
        if (result.stats != null)
        {
            _output.WriteLine($"  Investor Count: {result.stats.InvestorCount}");
            _output.WriteLine($"  Amount Invested: {result.stats.AmountInvested} sats");
            _output.WriteLine($"  Amount In Penalties: {result.stats.AmountInPenalties} sats");
            _output.WriteLine($"  Count In Penalties: {result.stats.CountInPenalties}");
        }
        else
        {
            _output.WriteLine("  No stats found (this may be expected if project doesn't exist)");
        }

        Assert.Equal(TestProjectId, result.projectId);
    }

    [Fact(Skip = "Manual test - requires active Electrum server and valid project")]
    public async Task GetInvestmentsAsync_WithValidProjectId_ReturnsInvestments()
    {
        _derivationOperations
     .Setup(x => x.ConvertAngorKeyToBitcoinAddress(TestProjectId))
      .Returns(TestProjectAddress);

        var result = await _service.GetInvestmentsAsync(TestProjectId);

        _output.WriteLine($"Investments for project {TestProjectId}:");
        _output.WriteLine($"Total investments: {result.Count}");

        foreach (var investment in result.Take(5))
        {
            _output.WriteLine($"  Transaction ID: {investment.TransactionId}");
            _output.WriteLine($"  Investor Public Key: {investment.InvestorPublicKey}");
            _output.WriteLine($"  Hash of Secret: {investment.HashOfSecret}");
            _output.WriteLine("  ---");
        }

        Assert.NotNull(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server and valid project/investor")]
    public async Task GetInvestmentAsync_WithValidProjectAndInvestor_ReturnsInvestment()
    {
        _derivationOperations
          .Setup(x => x.ConvertAngorKeyToBitcoinAddress(TestProjectId))
                 .Returns(TestProjectAddress);

        var result = await _service.GetInvestmentAsync(TestProjectId, TestInvestorPubKey);

        _output.WriteLine($"Investment for project {TestProjectId} by investor {TestInvestorPubKey}:");
        if (result != null)
        {
            _output.WriteLine($"  Transaction ID: {result.TransactionId}");
            _output.WriteLine($"  Investor Public Key: {result.InvestorPublicKey}");
            _output.WriteLine($"  Hash of Secret: {result.HashOfSecret}");
        }
        else
        {
            _output.WriteLine("  No investment found");
        }

        Assert.True(result == null || !string.IsNullOrEmpty(result.TransactionId));
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetProjectByIdAsync_WithEmptyProjectId_ReturnsNull()
    {
        var result = await _service.GetProjectByIdAsync("");

        _output.WriteLine("Empty project ID should return null");
        Assert.Null(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetProjectByIdAsync_WithShortProjectId_ReturnsNull()
    {
        var result = await _service.GetProjectByIdAsync("a");

        _output.WriteLine("Short project ID (length <= 1) should return null");
        Assert.Null(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetProjectStatsAsync_WithEmptyProjectId_ReturnsNullStats()
    {
        var result = await _service.GetProjectStatsAsync("");

        _output.WriteLine("Empty project ID should return null stats");
        Assert.Equal("", result.projectId);
        Assert.Null(result.stats);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetInvestmentsAsync_WithEmptyProjectId_ReturnsEmptyList()
    {
        var result = await _service.GetInvestmentsAsync("");

        _output.WriteLine("Empty project ID should return empty list");
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetInvestmentAsync_WithEmptyProjectId_ReturnsNull()
    {
        var result = await _service.GetInvestmentAsync("", TestInvestorPubKey);

        _output.WriteLine("Empty project ID should return null");
        Assert.Null(result);
    }

    [Fact(Skip = "Manual test - requires active Electrum server")]
    public async Task GetInvestmentAsync_WithEmptyInvestorPubKey_ReturnsNull()
    {
        var result = await _service.GetInvestmentAsync(TestProjectId, "");

        _output.WriteLine("Empty investor public key should return null");
        Assert.Null(result);
    }
}

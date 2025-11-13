using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.Services;

/// <summary>
/// Integration tests comparing Angor API vs Blockchain-based Calculation API.
/// These tests are disabled by default and should be enabled for live testing.
/// </summary>
public class IndexerComparisonTests
{
    private readonly MempoolSpaceIndexerApi _indexerApi;
    private readonly IAngorIndexerService _calculationApi;
    private readonly ILogger<IndexerComparisonTests> _logger;

    // Test configuration
    private const string TestProjectId = "angor1qrkuxpvnu4x0rtcaz0jyjqrt5gk7hnyz8q9dye5";
    private const string TestIndexerUrl = "https://signet.angor.online";

    public IndexerComparisonTests()
    {
        _logger = new NullLogger<IndexerComparisonTests>();

        // Setup mocks
        var mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        var mockNetworkStorage = new Mock<INetworkStorage>();
        var mockDerivationOperations = new Mock<IDerivationOperations>();

        // Configure network to use Signet
        mockNetworkConfiguration.Setup(nc => nc.GetNetwork()).Returns(Networks.Bitcoin.SigNet());
        mockNetworkConfiguration.Setup(nc => nc.GetAngorKey()).Returns("angor1qrkuxpvnu4x0rtcaz0jyjqrt5gk7hnyz8q9dye5");

        // Setup network storage with test indexer
        mockNetworkStorage.Setup(ns => ns.GetSettings()).Returns(new SettingsInfo
        {
            Indexers = new List<SettingsUrl>
            {
                new() { Name = "Signet Indexer", Url = TestIndexerUrl, IsPrimary = true }
            }
        });

        // Setup DerivationOperations mock
        mockDerivationOperations
            .Setup(d => d.ConvertAngorKeyToBitcoinAddress(It.IsAny<string>()))
            .Returns<string>(projectId =>
            {
                // For testing purposes, return a known address
                // In real scenario, this would derive the actual address
                return "tb1qrkuxpvnu4x0rtcaz0jyjqrt5gk7hnyz85xtdlj"; // Example Signet address
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
       .Returns(() => new HttpClient(new HttpClientHandler())
       {
           Timeout = TimeSpan.FromSeconds(30)
       });

        var networkService = new NetworkService(
            mockNetworkStorage.Object,
            mockHttpClientFactory.Object,
            new NullLogger<NetworkService>(),
            mockNetworkConfiguration.Object);

        var mappers = new MempoolIndexerMappers(new NullLogger<MempoolIndexerMappers>());

        _calculationApi = new MempoolIndexerAngorApi(
                            new NullLogger<MempoolIndexerAngorApi>(),
                            networkService,
                            mockDerivationOperations.Object,
                            mappers,
                            mockHttpClientFactory.Object);

        _indexerApi = new MempoolSpaceIndexerApi(
            new NullLogger<MempoolSpaceIndexerApi>(),
            mockHttpClientFactory.Object,
            networkService,
            mockDerivationOperations.Object,
            mappers,
            _calculationApi);
    }

    /// <summary>
    /// Test to compare GetProjectByIdAsync results between Angor API and Blockchain calculation.
    /// Uncomment [Fact] attribute to enable this test.
    /// </summary>
    //[Fact]
    public async Task CompareGetProjectByIdAsync_BothEndpointsShouldReturnSimilarData()
    {
        // Arrange
        _logger.LogInformation($"Testing project: {TestProjectId}");
        _logger.LogInformation($"Indexer URL: {TestIndexerUrl}");

        // Act - Call Angor API
        _calculationApi.ReadFromAngorApi = true;
        var angorApiResult = await _indexerApi.GetProjectByIdAsync(TestProjectId);

        // Act - Call Blockchain Calculation API
        _calculationApi.ReadFromAngorApi = false;
        var blockchainResult = await _indexerApi.GetProjectByIdAsync(TestProjectId);

        // Assert - Both should return data
        Assert.NotNull(angorApiResult);
        Assert.NotNull(blockchainResult);

        // Log results for comparison
        _logger.LogInformation("=== Angor API Results ===");
        LogProjectIndexerData(angorApiResult);

        _logger.LogInformation("=== Blockchain Calculation API Results ===");
        LogProjectIndexerData(blockchainResult);

        // Assert - Compare key fields
        Assert.Equal(angorApiResult.ProjectIdentifier, blockchainResult.ProjectIdentifier);
        Assert.Equal(angorApiResult.FounderKey, blockchainResult.FounderKey);
        Assert.Equal(angorApiResult.NostrEventId, blockchainResult.NostrEventId);
        Assert.Equal(angorApiResult.TrxId, blockchainResult.TrxId);
        Assert.Equal(angorApiResult.CreatedOnBlock, blockchainResult.CreatedOnBlock);

        // Investment count might differ slightly due to timing, but should be close
        var investmentCountDifference = Math.Abs((angorApiResult.TotalInvestmentsCount ?? 0) - (blockchainResult.TotalInvestmentsCount ?? 0));
        Assert.True(investmentCountDifference <= 1,
        $"Investment count difference too large. Angor API: {angorApiResult.TotalInvestmentsCount}, Blockchain: {blockchainResult.TotalInvestmentsCount}");
    }

    /// <summary>
    /// Test to compare GetProjectStatsAsync results between Angor API and Blockchain calculation.
    /// Uncomment [Fact] attribute to enable this test.
    /// </summary>
    //[Fact]
    public async Task CompareGetProjectStatsAsync_BothEndpointsShouldReturnSimilarStats()
    {
        // Arrange
        _logger.LogInformation($"Testing project stats: {TestProjectId}");

        // Act - Call Angor API
        _calculationApi.ReadFromAngorApi = true;
        var (apiProjectId, angorApiStats) = await _indexerApi.GetProjectStatsAsync(TestProjectId);

        // Act - Call Blockchain Calculation API
        _calculationApi.ReadFromAngorApi = false;
        var (blockchainProjectId, blockchainStats) = await _indexerApi.GetProjectStatsAsync(TestProjectId);

        // Assert - Both should return data
        Assert.Equal(TestProjectId, apiProjectId);
        Assert.Equal(TestProjectId, blockchainProjectId);

        if (angorApiStats != null && blockchainStats != null)
        {
            // Log results for comparison
            _logger.LogInformation("=== Angor API Stats ===");
            LogProjectStats(angorApiStats);

            _logger.LogInformation("=== Blockchain Calculation API Stats ===");
            LogProjectStats(blockchainStats);

            // Assert - Compare investor count
            // Note: Stats might differ slightly due to calculation methods
            var investorCountDifference = Math.Abs(angorApiStats.InvestorCount - blockchainStats.InvestorCount);
            Assert.True(investorCountDifference <= 1,
                  $"Investor count difference too large. Angor API: {angorApiStats.InvestorCount}, Blockchain: {blockchainStats.InvestorCount}");

            // Assert - Compare amount invested
            // Allow for small differences due to rounding or calculation methods
            var amountInvestedDifference = Math.Abs(angorApiStats.AmountInvested - blockchainStats.AmountInvested);
            var amountInvestedDifferencePercentage = angorApiStats.AmountInvested > 0 
                ? (double)amountInvestedDifference / angorApiStats.AmountInvested * 100 
                : 0;

            Assert.True(amountInvestedDifferencePercentage <= 1.0, // Allow up to 1% difference
                $"Amount invested difference too large. Angor API: {angorApiStats.AmountInvested}, " +
                $"Blockchain: {blockchainStats.AmountInvested}, Difference: {amountInvestedDifference} sats ({amountInvestedDifferencePercentage:F2}%)");

            // Log the comparison summary
            _logger.LogInformation("=== Comparison Summary ===");
            _logger.LogInformation($"Investor Count - Angor: {angorApiStats.InvestorCount}, Blockchain: {blockchainStats.InvestorCount}, Diff: {investorCountDifference}");
            _logger.LogInformation($"Amount Invested - Angor: {angorApiStats.AmountInvested}, Blockchain: {blockchainStats.AmountInvested}, Diff: {amountInvestedDifference} sats ({amountInvestedDifferencePercentage:F2}%)");
        }
        else
        {
            _logger.LogWarning("One or both stats are null. This might be expected for projects without investments.");
        }
    }

    /// <summary>
    /// Test to compare GetInvestmentsAsync results between Angor API and Blockchain calculation.
    /// Uncomment [Fact] attribute to enable this test.
    /// </summary>
    //[Fact]
    public async Task CompareGetInvestmentsAsync_BothEndpointsShouldReturnSimilarInvestments()
    {
        // Arrange
        _logger.LogInformation($"Testing project investments: {TestProjectId}");

        // Act - Call Angor API
        _calculationApi.ReadFromAngorApi = true;
        var angorApiInvestments = await _indexerApi.GetInvestmentsAsync(TestProjectId);

        // Act - Call Blockchain Calculation API
        _calculationApi.ReadFromAngorApi = false;
        var blockchainInvestments = await _indexerApi.GetInvestmentsAsync(TestProjectId);

        // Assert - Both should return data
        Assert.NotNull(angorApiInvestments);
        Assert.NotNull(blockchainInvestments);

        // Log results for comparison
        _logger.LogInformation($"=== Angor API Investments: {angorApiInvestments.Count} ===");
        foreach (var investment in angorApiInvestments.Take(5)) // Log first 5
        {
            LogProjectInvestment(investment);
        }

        _logger.LogInformation($"=== Blockchain Calculation API Investments: {blockchainInvestments.Count} ===");
        foreach (var investment in blockchainInvestments.Take(5)) // Log first 5
        {
            LogProjectInvestment(investment);
        }

        // Assert - Compare counts
        var countDifference = Math.Abs(angorApiInvestments.Count - blockchainInvestments.Count);
        Assert.True(countDifference <= 1,
       $"Investment count difference too large. Angor API: {angorApiInvestments.Count}, Blockchain: {blockchainInvestments.Count}");

        // If both have investments, compare the first few
        if (angorApiInvestments.Any() && blockchainInvestments.Any())
        {
            var commonCount = Math.Min(angorApiInvestments.Count, blockchainInvestments.Count);
            var matchingTransactions = 0;

            foreach (var apiInvestment in angorApiInvestments)
            {
                if (blockchainInvestments.Any(bi => bi.TransactionId == apiInvestment.TransactionId))
                {
                    matchingTransactions++;
                }
            }

            _logger.LogInformation($"Matching transactions: {matchingTransactions} out of {commonCount}");

            // At least 80% of transactions should match
            var matchPercentage = (double)matchingTransactions / commonCount * 100;
            Assert.True(matchPercentage >= 80,
         $"Too few matching transactions. Match percentage: {matchPercentage:F2}%");
        }
    }

    /// <summary>
    /// Test to compare GetInvestmentAsync results for a specific investor.
    /// Uncomment [Fact] attribute to enable this test.
    /// Note: You need to provide a valid investor public key for this test.
    /// </summary>
    //[Fact]
    public async Task CompareGetInvestmentAsync_BothEndpointsShouldReturnSameInvestment()
    {
        // Arrange
        // First, get investments to find a valid investor public key
        _calculationApi.ReadFromAngorApi = true;
        var investments = await _indexerApi.GetInvestmentsAsync(TestProjectId);

        if (!investments.Any())
        {
            _logger.LogWarning("No investments found for this project. Skipping specific investment test.");
            return;
        }

        var testInvestorPubKey = investments.First().InvestorPublicKey;
        _logger.LogInformation($"Testing specific investment for investor: {testInvestorPubKey}");

        // Act - Call Angor API
        _calculationApi.ReadFromAngorApi = true;
        var angorApiInvestment = await _indexerApi.GetInvestmentAsync(TestProjectId, testInvestorPubKey);

        // Act - Call Blockchain Calculation API
        _calculationApi.ReadFromAngorApi = false;
        var blockchainInvestment = await _indexerApi.GetInvestmentAsync(TestProjectId, testInvestorPubKey);

        // Assert - Both should return data
        Assert.NotNull(angorApiInvestment);
        Assert.NotNull(blockchainInvestment);

        // Log results for comparison
        _logger.LogInformation("=== Angor API Investment ===");
        LogProjectInvestment(angorApiInvestment);

        _logger.LogInformation("=== Blockchain Calculation API Investment ===");
        LogProjectInvestment(blockchainInvestment);

        // Assert - Compare investment details
        Assert.Equal(angorApiInvestment.TransactionId, blockchainInvestment.TransactionId);
        Assert.Equal(angorApiInvestment.InvestorPublicKey, blockchainInvestment.InvestorPublicKey);
    }

    /// <summary>
    /// Performance test comparing response times between both APIs.
    /// Uncomment [Fact] attribute to enable this test.
    /// </summary>
    //[Fact]
    public async Task ComparePerformance_MeasureResponseTimes()
    {
        // Arrange
        var angorApiTimes = new List<TimeSpan>();
        var blockchainApiTimes = new List<TimeSpan>();
        const int iterations = 3;

        _logger.LogInformation($"Running performance test with {iterations} iterations");

        // Act - Run multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            _logger.LogInformation($"Iteration {i + 1}/{iterations}");

            // Test Angor API
            _calculationApi.ReadFromAngorApi = true;
            var angorWatch = System.Diagnostics.Stopwatch.StartNew();
            await _indexerApi.GetProjectByIdAsync(TestProjectId);
            angorWatch.Stop();
            angorApiTimes.Add(angorWatch.Elapsed);
            _logger.LogInformation($"Angor API: {angorWatch.ElapsedMilliseconds}ms");

            // Small delay between calls
            await Task.Delay(100);

            // Test Blockchain API
            _calculationApi.ReadFromAngorApi = false;
            var blockchainWatch = System.Diagnostics.Stopwatch.StartNew();
            await _indexerApi.GetProjectByIdAsync(TestProjectId);
            blockchainWatch.Stop();
            blockchainApiTimes.Add(blockchainWatch.Elapsed);
            _logger.LogInformation($"Blockchain API: {blockchainWatch.ElapsedMilliseconds}ms");

            await Task.Delay(100);
        }

        // Calculate averages
        var angorAverage = TimeSpan.FromMilliseconds(angorApiTimes.Average(t => t.TotalMilliseconds));
        var blockchainAverage = TimeSpan.FromMilliseconds(blockchainApiTimes.Average(t => t.TotalMilliseconds));

        _logger.LogInformation("=== Performance Results ===");
        _logger.LogInformation($"Angor API Average: {angorAverage.TotalMilliseconds:F2}ms");
        _logger.LogInformation($"Blockchain API Average: {blockchainAverage.TotalMilliseconds:F2}ms");
        _logger.LogInformation($"Difference: {Math.Abs(angorAverage.TotalMilliseconds - blockchainAverage.TotalMilliseconds):F2}ms");

        // Assert - Both should complete within reasonable time (30 seconds)
        Assert.True(angorAverage.TotalSeconds < 30, "Angor API took too long");
        Assert.True(blockchainAverage.TotalSeconds < 30, "Blockchain API took too long");
    }

    #region Helper Methods

    private void LogProjectIndexerData(ProjectIndexerData data)
    {
        _logger.LogInformation($"Project Identifier: {data.ProjectIdentifier}");
        _logger.LogInformation($"Founder Key: {data.FounderKey}");
        _logger.LogInformation($"Nostr Event ID: {data.NostrEventId}");
        _logger.LogInformation($"Transaction ID: {data.TrxId}");
        _logger.LogInformation($"Created On Block: {data.CreatedOnBlock}");
        _logger.LogInformation($"Total Investments: {data.TotalInvestmentsCount ?? 0}");
    }

    private void LogProjectStats(ProjectStats stats)
    {
        _logger.LogInformation($"Investor Count: {stats.InvestorCount}");
        _logger.LogInformation($"Amount Invested: {stats.AmountInvested}");
        _logger.LogInformation($"Amount In Penalties: {stats.AmountInPenalties}");
        _logger.LogInformation($"Count In Penalties: {stats.CountInPenalties}");
    }

    private void LogProjectInvestment(ProjectInvestment investment)
    {
        _logger.LogInformation($"Transaction ID: {investment.TransactionId}");
        _logger.LogInformation($"Investor Public Key: {investment.InvestorPublicKey}");
        _logger.LogInformation($"Total Amount: {investment.TotalAmount}");
        _logger.LogInformation($"Hash Of Secret: {investment.HashOfSecret}");
        _logger.LogInformation($"Is Seeder: {investment.IsSeeder}");
        _logger.LogInformation("---");
    }

    #endregion
}

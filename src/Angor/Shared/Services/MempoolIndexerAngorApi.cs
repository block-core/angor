using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Angor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolIndexerAngorApi : IAngorIndexerService
{
    private readonly ILogger<MempoolIndexerAngorApi> _logger;
    private readonly INetworkService _networkService;
    private readonly IDerivationOperations _derivationOperations;
    private readonly MempoolIndexerMappers _mappers;
    private readonly IHttpClientFactory _clientFactory;

    private const string AngorApiRoute = "/api/v1/query/Angor";
    private const string MempoolApiRoute = "/api/v1";

    public bool ReadFromAngorApi { get; set; } = false;

    public MempoolIndexerAngorApi(
        ILogger<MempoolIndexerAngorApi> logger,
        INetworkService networkService,
        IDerivationOperations derivationOperations,
        MempoolIndexerMappers mappers,
        IHttpClientFactory clientFactory)
    {
        _logger = logger;
        _networkService = networkService;
        _derivationOperations = derivationOperations;
        _mappers = mappers;
        _clientFactory = clientFactory;
    }

    private HttpClient GetIndexerClient()
    {
        var indexer = _networkService.GetPrimaryIndexer();
        var client = _clientFactory.CreateClient();
        client.BaseAddress = new Uri(indexer.Url);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    public async Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit)
    {
        // GetProjectsAsync only supports Angor API (no blockchain equivalent for listing all projects)
        try
        {
            var url = offset == null ?
                $"{AngorApiRoute}/projects?limit={limit}" :
                $"{AngorApiRoute}/projects?offset={offset}&limit={limit}";

            var response = await GetIndexerClient().GetAsync(url);
            _networkService.CheckAndHandleError(response);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectIndexerData>>() ?? new List<ProjectIndexerData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching projects from Angor API: {ex.Message}");
            return new List<ProjectIndexerData>();
        }
    }

    public async Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return null;
        }

        if (projectId.Length <= 1)
        {
            return null;
        }

        if (ReadFromAngorApi)
        {
            // Call Angor API
            try
            {
                var response = await GetIndexerClient()
                    .GetAsync($"{AngorApiRoute}/projects/{projectId}");
                _networkService.CheckAndHandleError(response);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<ProjectIndexerData>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching project by ID from Angor API {projectId}: {ex.Message}");
                return null;
            }
        }

        // Fetch project info from Mempool.space API (blockchain-based)
        try
        {
            var projectAddress = _derivationOperations.ConvertAngorKeyToBitcoinAddress(projectId);

            var response = await GetIndexerClient().GetAsync($"{MempoolApiRoute}/address/{projectAddress}/txs");

            _networkService.CheckAndHandleError(response);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"No transactions found for project address {projectAddress}");
                return null;
            }

            var trxs = await response.Content.ReadFromJsonAsync<List<MempoolSpaceIndexerApi.MempoolTransaction>>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (trxs == null || !trxs.Any())
            {
                _logger.LogWarning($"No transactions found for project {projectId}");
                return null;
            }

            // Convert transactions to ProjectIndexerData using the mapper
            return _mappers.ConvertTransactionsToProjectIndexerData(projectId, trxs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching project by ID {projectId}: {ex.Message}");
            return null;
        }
    }

    public async Task<(string projectId, ProjectStats? stats)> GetProjectStatsAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return (projectId, null);
        }

        if (ReadFromAngorApi)
        {
            // Call Angor API
            try
            {
                var response = await GetIndexerClient()
                    .GetAsync($"{AngorApiRoute}/projects/{projectId}/stats");
                _networkService.CheckAndHandleError(response);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (projectId, null);
                }

                return (projectId, await response.Content.ReadFromJsonAsync<ProjectStats>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching project stats from Angor API for ID {projectId}: {ex.Message}");
                return (projectId, null);
            }
        }

        // Calculate stats from blockchain data
        try
        {
            var projectAddress = _derivationOperations.ConvertAngorKeyToBitcoinAddress(projectId);

            var response = await GetIndexerClient().GetAsync($"{MempoolApiRoute}/address/{projectAddress}/txs");

            _networkService.CheckAndHandleError(response);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"No transactions found for project address {projectAddress}");
                return (projectId, null);
            }

            var trxs = await response.Content.ReadFromJsonAsync<List<MempoolSpaceIndexerApi.MempoolTransaction>>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (trxs == null || !trxs.Any())
            {
                _logger.LogWarning($"No transactions found for project {projectId}");
                return (projectId, null);
            }

            // Calculate stats using the mapper
            var stats = _mappers.CalculateProjectStats(projectId, trxs);

            return (projectId, stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching project stats for ID {projectId}: {ex.Message}");
            return (projectId, null);
        }
    }

    public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
    {
        if (ReadFromAngorApi)
        {
            // Call Angor API
            try
            {
                var response = await GetIndexerClient()
                    .GetAsync($"{AngorApiRoute}/projects/{projectId}/investments?limit=50");
                _networkService.CheckAndHandleError(response);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>() ?? new List<ProjectInvestment>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching investments from Angor API for project {projectId}: {ex.Message}");
                return new List<ProjectInvestment>();
            }
        }

        // Get investments from blockchain data
        if (string.IsNullOrEmpty(projectId))
        {
            return new List<ProjectInvestment>();
        }

        try
        {
            var projectAddress = _derivationOperations.ConvertAngorKeyToBitcoinAddress(projectId);

            var response = await GetIndexerClient().GetAsync($"{MempoolApiRoute}/address/{projectAddress}/txs");

            _networkService.CheckAndHandleError(response);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"No transactions found for project address {projectAddress}");
                return new List<ProjectInvestment>();
            }

            var trxs = await response.Content.ReadFromJsonAsync<List<MempoolSpaceIndexerApi.MempoolTransaction>>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (trxs == null || !trxs.Any())
            {
                _logger.LogWarning($"No transactions found for project {projectId}");
                return new List<ProjectInvestment>();
            }

            // Use the mapper to convert transactions to investments
            return _mappers.ConvertTransactionsToInvestments(projectId, trxs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching investments for project {projectId}: {ex.Message}");
            return new List<ProjectInvestment>();
        }
    }

    public async Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey)
    {
        if (ReadFromAngorApi)
        {
            // Call Angor API
            try
            {
                var response = await GetIndexerClient()
                    .GetAsync($"{AngorApiRoute}/projects/{projectId}/investments/{investorPubKey}");
                _networkService.CheckAndHandleError(response);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProjectInvestment>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching investment from Angor API for project {projectId} and investor {investorPubKey}: {ex.Message}");
                return null;
            }
        }

        // Get specific investment from blockchain data
        if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(investorPubKey))
        {
            return null;
        }

        try
        {
            var investments = await GetInvestmentsAsync(projectId);

            // Find the investment matching the investor public key
            var investment = investments.FirstOrDefault(inv =>
                inv.InvestorPublicKey.Equals(investorPubKey, StringComparison.OrdinalIgnoreCase));

            if (investment == null)
            {
                _logger.LogWarning($"No investment found for project {projectId} and investor {investorPubKey}");
            }

            return investment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching investment for project {projectId} and investor {investorPubKey}: {ex.Message}");
            return null;
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Angor.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolIndexerCalculationApi : IMempoolIndexerCalculationApi
{
    private readonly ILogger<MempoolIndexerCalculationApi> _logger;
    private readonly INetworkService _networkService;
    private readonly IDerivationOperations _derivationOperations;
    private readonly MempoolIndexerMappers _mappers;
    private readonly IHttpClientFactory _clientFactory;

    private const string MempoolApiRoute = "/api/v1";

    public MempoolIndexerCalculationApi(
        ILogger<MempoolIndexerCalculationApi> logger,
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

        // Fetch project info from Mempool.space API
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

        try
        {
            var projectData = await GetProjectByIdAsync(projectId);

            if (projectData == null)
            {
                _logger.LogWarning($"No project data found for project {projectId}");
                return (projectId, null);
            }

            // Calculate stats from project data
            // todo: add more stats calculations as needed
            var stats = new ProjectStats
            {
                InvestorCount = projectData.TotalInvestmentsCount ?? 0
            };

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

            // Sort transactions by block height
            var sortedTransactions = trxs.OrderBy(t => t.Status.BlockHeight).ToList();

            // Find the funding transaction first
            MempoolSpaceIndexerApi.MempoolTransaction? fundingTrx = null;
            foreach (var trx in sortedTransactions)
            {
                if (trx.Vout.Count < 2)
                    continue;

                var opReturnOutput = trx.Vout[1];
                if (opReturnOutput.ScriptpubkeyType == "op_return" || opReturnOutput.ScriptpubkeyType == "nulldata")
                {
                    var parsedData = _mappers.ParseFounderInfoFromOpReturn(opReturnOutput.Scriptpubkey);
                    if (parsedData != null)
                    {
                        fundingTrx = trx;
                        break;
                    }
                }
            }

            if (fundingTrx == null)
            {
                _logger.LogWarning($"No funding transaction found for project {projectId}");
                return new List<ProjectInvestment>();
            }

            // Collect all investment transactions
            var investments = new List<ProjectInvestment>();
            foreach (var trx in sortedTransactions)
            {
                // Skip the funding transaction
                if (trx.Txid == fundingTrx.Txid)
                    continue;

                if (trx.Vout.Count < 2)
                    continue;

                var opReturnOutput = trx.Vout[1];
                if (opReturnOutput.ScriptpubkeyType == "op_return" || opReturnOutput.ScriptpubkeyType == "nulldata")
                {
                    try
                    {
                        var investorKey = _mappers.ParseInvestorInfoFromOpReturn(opReturnOutput.Scriptpubkey);
                        if (!string.IsNullOrEmpty(investorKey))
                        {
                            investments.Add(new ProjectInvestment
                            {
                                TransactionId = trx.Txid,
                                InvestorPublicKey = investorKey,
                                HashOfSecret = string.Empty // TODO: Extract from OP_RETURN if present
                            });

                            _logger.LogDebug($"Found investment transaction {trx.Txid} for project {projectId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"Failed to parse investor info from transaction {trx.Txid}");
                    }
                }
            }

            return investments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching investments for project {projectId}: {ex.Message}");
            return new List<ProjectInvestment>();
        }
    }

    public async Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey)
    {
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

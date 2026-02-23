using System.Net;
using System.Net.Http.Json;
using Angor.Shared.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;


namespace Angor.Shared.Services
{
    
    [Obsolete("This class is deprecated and will be removed in future versions. Please use the new MempoolSpaceIndexerApi instead.")]
    public class IndexerService : IIndexerService
    {
        private readonly INetworkConfiguration _networkConfiguration;
        private readonly HttpClient _httpClient;
        private readonly INetworkService _networkService;
        private readonly ILogger<IndexerService>? _logger;


        public IndexerService(INetworkConfiguration networkConfiguration, HttpClient httpClient, INetworkService networkService)
        {
            _networkConfiguration = networkConfiguration;
            _httpClient = httpClient;
            _networkService = networkService;
        }

        public async Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit)
        {
            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects?offset={offset}&limit={limit}");
            _networkService.CheckAndHandleError(response);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<List<ProjectIndexerData>>() ?? new List<ProjectIndexerData>();
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

            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects/{projectId}");
            _networkService.CheckAndHandleError(response);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ProjectIndexerData>();
        }

        public async Task<(string projectId, ProjectStats? stats)> GetProjectStatsAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return (projectId, null);
            }

            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects/{projectId}/stats");
            _networkService.CheckAndHandleError(response);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (projectId, null);
            }

            return (projectId,await response.Content.ReadFromJsonAsync<ProjectStats>());
        }

        public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
        {
            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects/{projectId}/investments");
            _networkService.CheckAndHandleError(response);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>();
        }

        public async Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey)
        {
            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects/{projectId}/investments/{investorPubKey}");
            _networkService.CheckAndHandleError(response);
            return response.IsSuccessStatusCode 
                ? await response.Content.ReadFromJsonAsync<ProjectInvestment>() 
                : null;
        }

        public async Task<string> PublishTransactionAsync(string trxHex)
        {
            var indexer = _networkService.GetPrimaryIndexer();

            var response = await _httpClient.PostAsync($"{indexer.Url}/api/command/send", new StringContent(trxHex));
            _networkService.CheckAndHandleError(response);

            if (response.IsSuccessStatusCode)
                return string.Empty;

            var content = await response.Content.ReadAsStringAsync();

            return response.ReasonPhrase + content;
        }

        public async Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false)
        {
            //check all new addresses for balance or a history
            var urlBalance = $"/api/query/addresses/balance?includeUnconfirmed={includeUnconfirmed}";
            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.PostAsJsonAsync(indexer.Url + urlBalance,
                data.Select(_ => _.Address).ToArray());
            _networkService.CheckAndHandleError(response);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var addressesNotEmpty = (await response.Content.ReadFromJsonAsync<AddressBalance[]>())?.ToArray() ?? Array.Empty<AddressBalance>();

            return addressesNotEmpty;
        }

        public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int offset, int limit)
        {
            var indexer = _networkService.GetPrimaryIndexer();

            var url = $"/api/query/address/{address}/transactions/unspent?confirmations=0&offset={offset}&limit={limit}";

            var response = await _httpClient.GetAsync(indexer.Url + url);
            _networkService.CheckAndHandleError(response);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var utxo = await response.Content.ReadFromJsonAsync<List<UtxoData>>();

            return utxo;
        }

        public Task<List<QueryTransaction>?> FetchAddressHistoryAsync(string address, string? afterTrxId)
        {
            throw new NotImplementedException();
        }

        public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
        {
            var indexer = _networkService.GetPrimaryIndexer();

            var url = confirmations.Aggregate("/api/stats/fee?", (current, block) => current + $@"confirmations={block}&");

            var response = await _httpClient.GetAsync(indexer.Url + url);
            _networkService.CheckAndHandleError(response);

            if (!response.IsSuccessStatusCode)
                // todo: uncomment this when the fee endpoint works
                //    throw new InvalidOperationException(response.ReasonPhrase);
                return null;

            var feeEstimations = await response.Content.ReadFromJsonAsync<FeeEstimations>();

            return feeEstimations;
        }

        public async Task<string> GetTransactionHexByIdAsync(string transactionId)
        {
            var indexer = _networkService.GetPrimaryIndexer();

            var url = $"/api/query/transaction/{transactionId}/hex";

            var response = await _httpClient.GetAsync(indexer.Url + url);
            _networkService.CheckAndHandleError(response);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
        {
            var indexer = _networkService.GetPrimaryIndexer();

            var url = $"/api/query/transaction/{transactionId}";

            var response = await _httpClient.GetAsync(indexer.Url + url);
            _networkService.CheckAndHandleError(response);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var info = await response.Content.ReadFromJsonAsync<QueryTransaction>();

            return info;
        }

        public Task<IEnumerable<(int, bool)>> GetIsSpentOutputsOnTransactionAsync(string transactionId)
        {
            throw new NotImplementedException();
        }


        public async Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl)
        {
            try
            {
                // fetch block 0 (Genesis Block)
                var blockUrl = $"{indexerUrl.TrimEnd('/')}/api/query/block/index/0";
        
                var blockResponse = await _httpClient.GetAsync(blockUrl);
        
                if (!blockResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch genesis block from: {blockUrl}");
                    return (false, null);
                }
        
                var responseContent = await blockResponse.Content.ReadAsStringAsync();
                var blockData = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
                if (blockData.TryGetProperty("blockHash", out JsonElement blockHashElement))
                {
                    return (true, blockHashElement.GetString());
                }
        
                _logger.LogWarning("blockHash not found in the block response.");
                return (true, null); // Indexer is online, but no valid block hash
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during indexer network check: {ex.Message}");
                return (false, null);
            }
        }

        
        public bool ValidateGenesisBlockHash(string fetchedHash, string expectedHash)
        {
            // Compare the first 64 characters (32 bytes in hex) of the fetched hash to the expected hash
            return fetchedHash.StartsWith(expectedHash, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(fetchedHash);
        }
        
    }
}

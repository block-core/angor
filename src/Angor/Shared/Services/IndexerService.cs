using System.Net;
using Angor.Shared.Models;
using Angor.Shared;
using System.Net.Http.Json;
using Angor.Shared.Services;
using static System.Net.WebRequestMethods;

namespace Angor.Client.Services
{
    public interface IIndexerService
    {
        Task<List<ProjectIndexerData>> GetProjectsAsync();
        Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId);
        Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId);
        Task<string> PublishTransactionAsync(string trxHex);
        Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false);
        Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset);
        Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations);

        Task<string> GetTransactionHexByIdAsync(string transactionId);

        Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId);
    }
    public class ProjectIndexerData
    {
        public string FounderKey { get; set; }
        public string ProjectIdentifier { get; set; }
        
        public long CreatedOnBlock { get; set; }
        public string NostrPubKey { get; set; }
    }

    public class ProjectInvestment
    {
        public string TransactionId { get; set; }
        
        public string InvestorPublicKey { get; set; }
        
        public long TotalAmount { get; set; }
        
        public string HashOfSecret { get; set; }

        public bool IsSeeder { get; set; }
    }

    public class IndexerService : IIndexerService
    {
        private readonly INetworkConfiguration _networkConfiguration;
        private readonly HttpClient _httpClient;
        private readonly INetworkService _networkService;

        public IndexerService(INetworkConfiguration networkConfiguration, HttpClient httpClient, INetworkService networkService)
        {
            _networkConfiguration = networkConfiguration;
            _httpClient = httpClient;
            _networkService = networkService;
        }

        public async Task<List<ProjectIndexerData>> GetProjectsAsync()
        {
            var indexer = _networkService.GetPrimaryIndexer();
            // todo: dan - make this proper paging
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects?offset=0&limit=50");
            _networkService.CheckAndHandleError(response);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectIndexerData>>();
        }

        public async Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId)
        {
            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects/{projectId}");
            _networkService.CheckAndHandleError(response);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            
            return await response.Content.ReadFromJsonAsync<ProjectIndexerData>();
        }

        public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
        {
            var indexer = _networkService.GetPrimaryIndexer();
            var response = await _httpClient.GetAsync($"{indexer.Url}/api/query/Angor/projects/{projectId}/investments");
            _networkService.CheckAndHandleError(response);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>();
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

        public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int offset , int limit)
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
    }
}

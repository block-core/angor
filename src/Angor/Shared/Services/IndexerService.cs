using Angor.Shared.Models;
using Angor.Shared;
using System.Net.Http.Json;
using static System.Net.WebRequestMethods;

namespace Angor.Client.Services
{
    public interface IIndexerService
    {
        Task<List<ProjectIndexerData>> GetProjectsAsync();
        Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId);
        Task<string> PublishTransactionAsync(string trxHex);
        Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data);
        Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset);
        Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations);

        Task<string> GetTransactionHexByIdAsync(string transactionId);

        Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId);
    }
    public class ProjectIndexerData
    {
        public string FounderKey { get; set; }
        public string ProjectIdentifier { get; set; }
        public string TrxId { get; set; }
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

        public IndexerService(INetworkConfiguration networkConfiguration, HttpClient httpClient)
        {
            _networkConfiguration = networkConfiguration;
            _httpClient = httpClient;
        }

        public async Task<List<ProjectIndexerData>> GetProjectsAsync()
        {
            var indexer = _networkConfiguration.GetIndexerUrl();
            // todo: dan - make this proper paging
            var response = await _httpClient.GetAsync($"{indexer.Url}/query/Angor/projects?offset=0&limit=50");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectIndexerData>>();
        }

        public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
        {
            var indexer = _networkConfiguration.GetIndexerUrl();
            var response = await _httpClient.GetAsync($"{indexer.Url}/query/Angor/projects/{projectId}/investments");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>();
        }

        public async Task<string> PublishTransactionAsync(string trxHex)
        {
            var indexer = _networkConfiguration.GetIndexerUrl();

            var endpoint = Path.Combine(indexer.Url, "command/send");

            var res = await _httpClient.PostAsync(endpoint, new StringContent(trxHex));

            if (res.IsSuccessStatusCode)
                return string.Empty;

            var content = await res.Content.ReadAsStringAsync();

            return res.ReasonPhrase + content;
        }

        public async Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data)
        {
            //check all new addresses for balance or a history
            var urlBalance = "/query/addresses/balance";
            var indexer = _networkConfiguration.GetIndexerUrl();
            var response = await _httpClient.PostAsJsonAsync(indexer.Url + urlBalance,
                data.Select(_ => _.Address).ToArray());

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var addressesNotEmpty = (await response.Content.ReadFromJsonAsync<AddressBalance[]>())?.ToArray() ?? Array.Empty<AddressBalance>();

            return addressesNotEmpty;
        }

        public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int offset , int limit)
        {
            SettingsUrl indexer = _networkConfiguration.GetIndexerUrl();

            var url = $"/query/address/{address}/transactions/unspent?confirmations=0&offset={offset}&limit={limit}";

            var response = await _httpClient.GetAsync(indexer.Url + url);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var utxo = await response.Content.ReadFromJsonAsync<List<UtxoData>>();

            return utxo;
        }

        public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
        {
            SettingsUrl indexer = _networkConfiguration.GetIndexerUrl();

            var url = confirmations.Aggregate("/stats/fee?", (current, block) => current + $@"confirmations={block}&");

            var response = await _httpClient.GetAsync(indexer.Url + url);

            
            if (!response.IsSuccessStatusCode)
                // todo: uncomment this when the fee endpoint works
                //    throw new InvalidOperationException(response.ReasonPhrase);
                return null;

            var feeEstimations = await response.Content.ReadFromJsonAsync<FeeEstimations>();

            return feeEstimations;
        }

        public async Task<string> GetTransactionHexByIdAsync(string transactionId)
        {
            SettingsUrl indexer = _networkConfiguration.GetIndexerUrl();

            var url = $"/query/transaction/{transactionId}/hex";
            
            var response = await _httpClient.GetAsync(indexer.Url + url);
            
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
        {
            SettingsUrl indexer = _networkConfiguration.GetIndexerUrl();

            var url = $"/query/transaction/{transactionId}";
            
            var response = await _httpClient.GetAsync(indexer.Url + url);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var info = await response.Content.ReadFromJsonAsync<QueryTransaction>();

            return info;
        }
    }
}

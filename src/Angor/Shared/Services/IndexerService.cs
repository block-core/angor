using Angor.Shared.Models;
using Angor.Shared;
using System.Net.Http;
using System.Net.Http.Json;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Networks;
using static System.Net.WebRequestMethods;
using System.Collections.Generic;

namespace Angor.Client.Services
{
    public interface IIndexerService
    {
        Task<List<ProjectIndexerData>> GetProjectsAsync();
        Task AddProjectAsync(ProjectIndexerData project);
        Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId);
        Task AddInvestmentAsync(ProjectInvestment project);
        Task<string> PublishTransactionAsync(string trxHex);
        Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data);
        Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset);

        Task<FeeEstimations?> GetFeeEstimation(int[] confirmations);

    }
    public class ProjectIndexerData
    {
        public string FounderKey { get; set; }
        public string ProjectIdentifier { get; set; }
        public string TrxHex { get; set; }
    }

    public class ProjectInvestment
    {
        public string ProjectIdentifier { get; set; }
        public string TrxId { get; set; }
        public string TrxHex { get; set; }
    }

    public class IndexerService : IIndexerService
    {
        private readonly INetworkConfiguration _networkConfiguration;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "/api/TestIndexer"; // "https://your-base-url/api/test";

        public IndexerService(INetworkConfiguration networkConfiguration, HttpClient httpClient)
        {
            _networkConfiguration = networkConfiguration;
            _httpClient = httpClient;
        }

        public async Task<List<ProjectIndexerData>> GetProjectsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectIndexerData>>();
        }

        public async Task AddProjectAsync(ProjectIndexerData project)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}", project);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/investment/{projectId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>();
        }

        public async Task AddInvestmentAsync(ProjectInvestment project)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/investment", project);
            response.EnsureSuccessStatusCode();
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
            IndexerUrl indexer = _networkConfiguration.GetIndexerUrl();

            var url = $"/query/address/{address}/transactions/unspent?confirmations=0&offset={offset}&limit={limit}";

            var response = await _httpClient.GetAsync(indexer.Url + url);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.ReasonPhrase);

            var utxo = await response.Content.ReadFromJsonAsync<List<UtxoData>>();

            return utxo;
        }

        public async Task<FeeEstimations?> GetFeeEstimation(int[] confirmations)
        {
            IndexerUrl indexer = _networkConfiguration.GetIndexerUrl();

            var url = confirmations.Aggregate("/stats/fee?", (current, block) => current + $@"confirmations={block}&");

            var response = await _httpClient.GetAsync(indexer.Url + url);

            
            if (!response.IsSuccessStatusCode)
                // todo: uncomment this when the fee endpoint works
                //    throw new InvalidOperationException(response.ReasonPhrase);
                return null;

            var feeEstimations = await response.Content.ReadFromJsonAsync<FeeEstimations>();

            return feeEstimations;
        }
    }
}

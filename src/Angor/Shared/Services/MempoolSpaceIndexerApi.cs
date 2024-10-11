using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolSpaceIndexerApi : IIndexerService
{
    private readonly ILogger<MempoolSpaceIndexerApi> _logger;
    
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly HttpClient _httpClient;
    private readonly INetworkService _networkService;

    private const string AngorApiRoute = "/api/V1/query/Angor";

    public MempoolSpaceIndexerApi(ILogger<MempoolSpaceIndexerApi> logger, INetworkConfiguration networkConfiguration, HttpClient httpClient, INetworkService networkService)
    {
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _httpClient = httpClient;
        _networkService = networkService;
    }

    public async Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit)
    {
        var indexer = _networkService.GetPrimaryIndexer();
        var response = await _httpClient.GetAsync($"{indexer.Url}{AngorApiRoute}/projects?offset={offset}&limit={limit}");
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
        var response = await _httpClient.GetAsync($"{indexer.Url}{AngorApiRoute}/projects/{projectId}");
        _networkService.CheckAndHandleError(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
            
        return await response.Content.ReadFromJsonAsync<ProjectIndexerData>();
    }

    public async Task<ProjectStats?> GetProjectStatsAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            return null;
        }

        var indexer = _networkService.GetPrimaryIndexer();
        var response = await _httpClient.GetAsync($"{indexer.Url}{AngorApiRoute}/projects/{projectId}/stats");
        _networkService.CheckAndHandleError(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProjectStats>();
    }

    public async Task<List<ProjectInvestment>> GetInvestmentsAsync(string projectId)
    {
        var indexer = _networkService.GetPrimaryIndexer();
        var response = await _httpClient.GetAsync($"{indexer.Url}{AngorApiRoute}/projects/{projectId}/investments");
        _networkService.CheckAndHandleError(response);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ProjectInvestment>>();
    }

    public async Task<string> PublishTransactionAsync(string trxHex)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var response = await _httpClient.PostAsync($"{indexer.Url}/api/tx", new StringContent(trxHex));
        _networkService.CheckAndHandleError(response);
            
        if (response.IsSuccessStatusCode)
        {    var txId = await response.Content.ReadAsStringAsync(); //The txId
            _logger.LogInformation("trx " + txId + "posted ");
            return string.Empty;
        }
        

        var content = await response.Content.ReadAsStringAsync();

        return response.ReasonPhrase + content;
    }

    private class AddressStats
    {
        public int FundedTxCount { get; set; }
        public long FundedTxoSum { get; set; }
        public int SpentTxocount { get; set; }
        public long SpentTxoSum { get; set; }
        public int TxCount { get; set; }
    }
    private class AddressResponse
    {
        public string Address { get; set; }
        public AddressStats ChainStats { get; set; }
        public AddressStats MempoolStats { get; set; }
    }
    
    public async Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false)
    {
        var urlBalance = $"/api/address/";

        var tasks = data.Select(x =>
        {
            //check all new addresses for balance or a history

            var indexer = _networkService.GetPrimaryIndexer();
            return _httpClient.GetAsync(indexer.Url + urlBalance + x.Address);
        });

        var results = await Task.WhenAll(tasks);

        var response = new List<AddressBalance>();
        
        foreach (var apiResponse in results)
        {
            _networkService.CheckAndHandleError(apiResponse);

            if (!apiResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(apiResponse.ReasonPhrase);

            var addressResponse = await apiResponse.Content.ReadFromJsonAsync<AddressResponse>(new JsonSerializerOptions()
                { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (addressResponse  != null && addressResponse.ChainStats.FundedTxoSum > addressResponse?.ChainStats.SpentTxoSum)
            {
                response.Add(new AddressBalance
                {
                    address = addressResponse.Address,
                    balance = addressResponse.ChainStats.FundedTxoSum - addressResponse.ChainStats.SpentTxoSum
                });
            }
        }
        
        return response.ToArray();
    }

    private class AddressUtxo
    {
        public string Txid { get; set; }
        public int Vout { get; set; }
        public UtxoStatus Status { get; set; }
        public long Value { get; set; }
    }
    
    private class UtxoStatus
    {
        public bool Confirmed { get; set; }
        public int BlockHeight { get; set; }
        public string BlockHash { get; set; }
        public long BlockTime { get; set; }
    }
    
    private class RecommendedFees
    {
        public int FastestFee { get; set; }
        public int HalfHourFee { get; set; }
        public int HourFee { get; set; }
        public int EconomyFee { get; set; }
        public int MinimumFee { get; set; }
    }
    
    private class MempoolTransaction : Transaction
    {
        public UtxoStatus Status { get; set; }
    }
    
    public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"/api/address/{address}/utxo";

        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        var utxo = await response.Content.ReadFromJsonAsync<List<AddressUtxo>>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        return utxo.Select(x => new UtxoData
        {
            address = address,
            scriptHex = null, //TODO!!
            blockIndex = x.Status.BlockHeight,
            outpoint = new Outpoint(x.Txid,x.Vout),
            value = x.Value,
            PendingSpent = !x.Status.Confirmed
        }).ToList();
    }

    public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = "/api/v1/fees/recommended";

        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"Error code {response.StatusCode}, {response.ReasonPhrase}");
            return null;
        }

        var feeEstimations = await response.Content.ReadFromJsonAsync<RecommendedFees>(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return new FeeEstimations
        {
            Fees = new List<FeeEstimation>
            {
                new() { FeeRate = feeEstimations.FastestFee, Confirmations = 1 }, //TODO this is an estimation
                new() { FeeRate = feeEstimations.HalfHourFee, Confirmations = 3 },
                new() { FeeRate = feeEstimations.HourFee, Confirmations = 6 },
                new() { FeeRate = feeEstimations.EconomyFee, Confirmations = 18 }, //TODO this is an estimation
            }
        };
    }

    public async Task<string> GetTransactionHexByIdAsync(string transactionId)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"/api/tx/{transactionId}/hex";
            
        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"/api/tx/{transactionId}/hex";
            
        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        var trx = await response.Content.ReadFromJsonAsync<MempoolTransaction>(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return new QueryTransaction
        {
            TransactionId = trx.GetHash().ToString(),
            Timestamp = trx.Status.BlockTime,
            Inputs = trx.Inputs.Select((x, i) => new QueryTransactionInput
            {
                InputIndex = i,
                InputTransactionId = x.PrevOut.Hash.ToString(),
                WitScript = x.WitScript.ToString()
            }),
            Outputs = trx.Outputs.Select((x, i) => new QueryTransactionOutput
            {
                Address = x.ScriptPubKey.PaymentScript.ToHex(), //TODO check that this is correct
                Balance = x.Value,
                Index = i,
                ScriptPubKey = x.ScriptPubKey.ToHex(),
                SpentInTransaction = trx.GetHash().ToString()
            })
        };
    }
}
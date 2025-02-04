using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolSpaceIndexerApi : IIndexerService
{
    private readonly ILogger<MempoolSpaceIndexerApi> _logger;
    
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly HttpClient _httpClient;
    private readonly INetworkService _networkService;

    private const string AngorApiRoute = "/api/V1/query/Angor";
    private const string MempoolApiRoute = "/api/V1";

    public MempoolSpaceIndexerApi(ILogger<MempoolSpaceIndexerApi> logger, INetworkConfiguration networkConfiguration, HttpClient httpClient, INetworkService networkService)
    {
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _httpClient = httpClient;
        _networkService = networkService;
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
    
    private class Vin
    {
        public bool IsCoinbase { get; set; }
        public PrevOut Prevout { get; set; }
        public string Scriptsig { get; set; }
        public string Asm { get; set; }
        public long Sequence { get; set; }
        public string Txid { get; set; }
        public int Vout { get; set; }
        public List<string> Witness { get; set; }
        public string InnserRedeemscriptAsm { get; set; }
        public string InnerWitnessscriptAsm { get; set; }
    }
    private class PrevOut
    {
        public long Value { get; set; }
        public string Scriptpubkey { get; set; }
        public string ScriptpubkeyAddress { get; set; }
        public string ScriptpubkeyAsm { get; set; }
        public string ScriptpubkeyType { get; set; }
    }

    private class MempoolTransaction
    {
        public string Txid { get; set; }

        public int Version { get; set; }

        public int Locktime { get; set; }
        public int Size { get; set; }
        public int Weight { get; set; }
        public int Fee { get; set; }
        public List<Vin> Vin { get; set; }
        public List<PrevOut> Vout { get; set; }
        public UtxoStatus Status { get; set; }
    }
    
    private class Outspent
    {
        public bool Spent { get; set; }
    }
    
    public async Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit)
    {
        var indexer = _networkService.GetPrimaryIndexer();
        var response = await _httpClient.GetAsync($"{indexer.Url}{AngorApiRoute}/projects?offset={offset ?? 0}&limit={limit}");
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

    public async Task<ProjectInvestment?> GetInvestmentAsync(string projectId, string investorPubKey)
    {
        var indexer = _networkService.GetPrimaryIndexer();
        var response = await _httpClient.GetAsync($"{indexer.Url}{AngorApiRoute}/projects/{projectId}/investments/{investorPubKey}");
        _networkService.CheckAndHandleError(response);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectInvestment>();
    }

    public async Task<string> PublishTransactionAsync(string trxHex)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var response = await _httpClient.PostAsync($"{indexer.Url}{MempoolApiRoute}/tx", new StringContent(trxHex));
        
        _networkService.CheckAndHandleError(response);
            
        if (response.IsSuccessStatusCode)
        {    
            var txId = await response.Content.ReadAsStringAsync(); //The txId
            _logger.LogInformation("trx " + txId + "posted ");
            return string.Empty;
        }

        var content = await response.Content.ReadAsStringAsync();

        return response.ReasonPhrase + content;
    }

    
    
    public async Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false)
    {
        var urlBalance = $"{MempoolApiRoute}/address/";

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

            if (addressResponse is { ChainStats.TxCount: > 0 })
            {
                response.Add(new AddressBalance
                {
                    address = addressResponse.Address,
                    balance = addressResponse.ChainStats.FundedTxoSum - addressResponse.ChainStats.SpentTxoSum,
                    pendingReceived = addressResponse.MempoolStats.FundedTxoSum - addressResponse.MempoolStats.SpentTxoSum 
                });
            }
        }
        
        return response.ToArray();
    }

    

    //To use if the UTXO endpoint works on the mempool indexer
    // public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset)
    // {
    //     var indexer = _networkService.GetPrimaryIndexer();
    //
    //     var url = $"/api/v1/address/{address}/utxo";
    //
    //     var response = await _httpClient.GetAsync(indexer.Url + url);
    //     _networkService.CheckAndHandleError(response);
    //
    //     if (!response.IsSuccessStatusCode)
    //         throw new InvalidOperationException(response.ReasonPhrase);
    //
    //     var utxo = await response.Content.ReadFromJsonAsync<List<AddressUtxo>>(new JsonSerializerOptions()
    //         { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    //
    //     return utxo.Select(x => new UtxoData
    //     {
    //         address = address,
    //         scriptHex = null, //TODO!!
    //         blockIndex = x.Status.BlockHeight,
    //         outpoint = new Outpoint(x.Txid,x.Vout),
    //         value = x.Value,
    //         PendingSpent = !x.Status.Confirmed
    //     }).ToList();
    // }
    
    public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"{MempoolApiRoute}/address/{address}/txs";

        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        var trx = await response.Content.ReadFromJsonAsync<List<MempoolTransaction>>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        var utxoDataList = new List<UtxoData>();
        
        foreach (var mempoolTransaction in trx)
        {
            var resultsOutputs =
                await _httpClient.GetAsync(indexer.Url + $"{MempoolApiRoute}/tx/" + mempoolTransaction.Txid + "/outspends");
        
            var spentOutputsStatus = await resultsOutputs.Content.ReadFromJsonAsync<List<Outspent>>();
        
            utxoDataList.AddRange(
                mempoolTransaction.Vout.Select(
                        (vout, i) =>
                        {
                            if (spentOutputsStatus[i].Spent || vout.ScriptpubkeyAddress != address)
                                return null;
                            
                            return new UtxoData
                            {
                                address = address,
                                scriptHex = vout.Scriptpubkey,
                                blockIndex = mempoolTransaction.Status.BlockHeight,
                                outpoint = new Outpoint(mempoolTransaction.Txid, i),
                                value = vout.Value,
                                PendingSpent = !mempoolTransaction.Status.Confirmed
                            };
                        }).Where(x => x != null)
                    .ToArray()
            );
        }
        return utxoDataList;
    }

    public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"{MempoolApiRoute}/fees/recommended";

        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"Error code {response.StatusCode}, {response.ReasonPhrase}");
            return null;
        }

        var feeEstimations = await response.Content.ReadFromJsonAsync<RecommendedFees>(new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        return new FeeEstimations
        {
            Fees = new List<FeeEstimation>
            {
                new() { FeeRate = feeEstimations.FastestFee * 1000, Confirmations = 1 }, //TODO this is an estimation
                new() { FeeRate = feeEstimations.HalfHourFee * 1000, Confirmations = 3 },
                new() { FeeRate = feeEstimations.HourFee * 1000, Confirmations = 6 },
                new() { FeeRate = feeEstimations.EconomyFee * 1000, Confirmations = 18 }, //TODO this is an estimation
            }
        };
    }

    public async Task<string> GetTransactionHexByIdAsync(string transactionId)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"{MempoolApiRoute}/tx/{transactionId}/hex";
            
        var response = await _httpClient.GetAsync(indexer.Url + url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
    {
        var indexer = _networkService.GetPrimaryIndexer();

        var url = $"{MempoolApiRoute}/tx/{transactionId}";
            
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
            TransactionId = trx.Txid,
            Timestamp = trx.Status.BlockTime,
            Inputs = trx.Vin.Select((x, i) => new QueryTransactionInput
            {
                InputIndex = i,
                InputTransactionId = x.Txid,
                WitScript = new WitScript(x.Witness.Select(s => Encoders.Hex.DecodeData(s)).ToArray()).ToString() 
            }),
            Outputs = trx.Vout
                .Select((x, i) => new QueryTransactionOutput
            {
                Address = x.ScriptpubkeyAddress, //TODO check that this is correct
                Balance = x.Value,
                Index = i,
                ScriptPubKey = x.Scriptpubkey,
                OutputType = x.ScriptpubkeyType,
                ScriptPubKeyAsm = x.ScriptpubkeyAsm
            })
        };
    }

    public async Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl)
    {
        try
        {
            // fetch block 0 (Genesis Block)
            var blockUrl = $"{indexerUrl}{MempoolApiRoute}/block-height/0";
        
            var blockResponse = await _httpClient.GetAsync(blockUrl);
        
            if (!blockResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to fetch genesis block from: {blockUrl}");
                return (false, null);
            }
        
            var blockHash = await blockResponse.Content.ReadAsStringAsync();
            return (true, blockHash); // Indexer is online, but no valid block hash
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during indexer network check: {ex.Message}");
            return (false, null);
        }
    }

    public bool ValidateGenesisBlockHash(string fetchedHash, string expectedHash)
    {
        return fetchedHash.StartsWith(expectedHash, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(fetchedHash);
    }
}
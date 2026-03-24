using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolSpaceIndexerApi : IIndexerService
{
    private readonly ILogger<MempoolSpaceIndexerApi> _logger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly INetworkService _networkService;
    private readonly IDerivationOperations _derivationOperations;

    private ConcurrentDictionary<string, HttpClient> _clients = new();

    private const string MempoolApiRoute = "/api/v1";

    public MempoolSpaceIndexerApi(
        ILogger<MempoolSpaceIndexerApi> logger,
        IHttpClientFactory clientFactory,
        INetworkService networkService,
        IDerivationOperations derivationOperations)
    {
        _logger = logger;
        _clientFactory = clientFactory;
        _networkService = networkService;
        _derivationOperations = derivationOperations;
    }

    #region Mempool.space API Models

    public class AddressStats
    {
        public int FundedTxCount { get; set; }
        public long FundedTxoSum { get; set; }
        public int SpentTxocount { get; set; }
        public long SpentTxoSum { get; set; }
        public int TxCount { get; set; }
    }
    public class AddressResponse
    {
        public string Address { get; set; }
        public AddressStats ChainStats { get; set; }
        public AddressStats MempoolStats { get; set; }
    }

    public class AddressUtxo
    {
        public string Txid { get; set; }
        public int Vout { get; set; }
        public UtxoStatus Status { get; set; }
        public long Value { get; set; }
    }

    public class UtxoStatus
    {
        public bool Confirmed { get; set; }
        public int BlockHeight { get; set; }
        public string BlockHash { get; set; }
        public long BlockTime { get; set; }
    }

    public class RecommendedFees
    {
        public int FastestFee { get; set; }
        public int HalfHourFee { get; set; }
        public int HourFee { get; set; }
        public int EconomyFee { get; set; }
        public int MinimumFee { get; set; }
    }

    public class Vin
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
    public class PrevOut
    {
        public long Value { get; set; }
        public string Scriptpubkey { get; set; }
        public string ScriptpubkeyAddress { get; set; }
        public string ScriptpubkeyAsm { get; set; }
        public string ScriptpubkeyType { get; set; }
    }

    public class MempoolTransaction
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
        public string Txid { get; set; }
        public int Vin { get; set; }
        public UtxoStatus Status { get; set; }
    }
    #endregion

    private HttpClient GetIndexerClient()
    {
        var indexer = _networkService.GetPrimaryIndexer();
        var key = string.IsNullOrEmpty(indexer.Name) ? indexer.Name : indexer.Url;
        if (_clients.TryGetValue(key, out var indexerClient))
            return indexerClient;

        var client = _clientFactory.CreateClient(key);
        client.BaseAddress = new Uri(indexer.Url);
        client.Timeout = TimeSpan.FromSeconds(10);

        _clients.TryAdd(key, client);

        return client;
    }

    public async Task<string> PublishTransactionAsync(string trxHex)
    {
        var response = await GetIndexerClient().PostAsync($"{MempoolApiRoute}/tx", new StringContent(trxHex));

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
        var client = GetIndexerClient(); // Call once, reuse for all requests

        var tasks = data.Select(x => client.GetAsync(urlBalance + x.Address));

        var results = await Task.WhenAll(tasks);

        var response = new List<AddressBalance>();

        foreach (var apiResponse in results)
        {
            _networkService.CheckAndHandleError(apiResponse);

            if (!apiResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(apiResponse.ReasonPhrase);

            var addressResponse = await apiResponse.Content.ReadFromJsonAsync<AddressResponse>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (addressResponse != null && (addressResponse.ChainStats.TxCount > 0 || addressResponse.MempoolStats.TxCount > 0))
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

    public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset)
    {
        var client = GetIndexerClient(); // Call once, reuse for all requests
        var txsUrl = $"{MempoolApiRoute}/address/{address}/txs";

        var response = await client.GetAsync(txsUrl);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        var trx = await response.Content.ReadFromJsonAsync<List<MempoolTransaction>>(new JsonSerializerOptions()
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        var utxoDataList = new List<UtxoData>();

        foreach (var mempoolTransaction in trx)
        {
            if (mempoolTransaction.Vout.All(v => v.ScriptpubkeyAddress != address))
            {
                // this trx has no outputs with the requested address.
                continue;
            }

            var outspendsUrl = $"{MempoolApiRoute}/tx/" + mempoolTransaction.Txid + "/outspends";

            var resultsOutputs = await client.GetAsync(outspendsUrl);

            var spentOutputsStatus = await resultsOutputs.Content.ReadFromJsonAsync<List<Outspent>>(new JsonSerializerOptions()
            { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            for (int index = 0; index < mempoolTransaction.Vout.Count; index++)
            {
                var vout = mempoolTransaction.Vout[index];

                if (vout.ScriptpubkeyAddress == address)
                {
                    if (mempoolTransaction.Status.Confirmed && spentOutputsStatus![index].Spent)
                    {
                        continue;
                    }

                    var data = new UtxoData
                    {
                        address = vout.ScriptpubkeyAddress,
                        scriptHex = vout.Scriptpubkey,
                        outpoint = new Outpoint(mempoolTransaction.Txid, index),
                        value = vout.Value,
                    };

                    if (mempoolTransaction.Status.Confirmed)
                    {
                        data.blockIndex = mempoolTransaction.Status.BlockHeight;
                    }

                    if (spentOutputsStatus![index].Spent)
                    {
                        data.PendingSpent = true;
                    }

                    utxoDataList.Add(data);
                }
            }
        }

        return utxoDataList;
    }

    public async Task<List<QueryTransaction>?> FetchAddressHistoryAsync(string address, string? afterTrxId = null) //TODO check the paging (I think it is 50 by default 
    {
        var txsUrl = $"{MempoolApiRoute}/address/{address}/txs";

        if (!string.IsNullOrEmpty(afterTrxId))
            txsUrl += $"?after_txid={afterTrxId}";

        var response = await GetIndexerClient()
                  .GetAsync(txsUrl);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        var trx = await response.Content.ReadFromJsonAsync<List<MempoolTransaction>>(new JsonSerializerOptions()
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

        return trx?.Select(t => MapToQueryTransaction(t)).ToList() ?? new List<QueryTransaction>();
    }

    public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
    {
        var url = $"{MempoolApiRoute}/fees/recommended";

        var response = await GetIndexerClient().GetAsync(url);
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
                new() { FeeRate = feeEstimations.FastestFee * 1100, Confirmations = 1 }, //TODO this is an estimation
                new() { FeeRate = feeEstimations.HalfHourFee * 1100, Confirmations = 3 },
                new() { FeeRate = feeEstimations.HourFee * 1100, Confirmations = 6 },
                new() { FeeRate = feeEstimations.EconomyFee * 1100, Confirmations = 18 }, //TODO this is an estimation
            }
        };
    }

    public async Task<string> GetTransactionHexByIdAsync(string transactionId)
    {
        var url = $"{MempoolApiRoute}/tx/{transactionId}/hex";

        var response = await GetIndexerClient()
          .GetAsync(url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
    {
        var client = GetIndexerClient(); // Call once, reuse for all requests
        var url = $"{MempoolApiRoute}/tx/{transactionId}";

        var response = await client.GetAsync(url);
        _networkService.CheckAndHandleError(response);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.ReasonPhrase);

        var options = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var trx = await response.Content.ReadFromJsonAsync<MempoolTransaction>(options);

        var urlSpent = $"{MempoolApiRoute}/tx/{transactionId}/outspends";

        var responseSpent = await client.GetAsync(urlSpent);
        _networkService.CheckAndHandleError(responseSpent);

        if (!responseSpent.IsSuccessStatusCode)
            throw new InvalidOperationException(responseSpent.ReasonPhrase);

        var spends = await responseSpent.Content.ReadFromJsonAsync<List<Outspent>>(options);

        await PopulateSpentMissingData(spends, trx, client);

        return MapToQueryTransaction(trx, spends);
    }

    public async Task<IEnumerable<(int, bool)>> GetIsSpentOutputsOnTransactionAsync(string transactionId)
    {
        var urlSpent = $"{MempoolApiRoute}/tx/{transactionId}/outspends";

        var responseSpent = await GetIndexerClient()
            .GetAsync(urlSpent);
        _networkService.CheckAndHandleError(responseSpent);

        var options = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var spends = await responseSpent.Content.ReadFromJsonAsync<List<Outspent>>(options);

        return spends!.Select((s, i) => (i, s.Spent));
    }

    private static QueryTransaction MapToQueryTransaction(MempoolTransaction x, List<Outspent>? spends = null)
    {
        return new QueryTransaction
        {
            BlockHash = x.Status.BlockHash,
            BlockIndex = x.Status.BlockHeight,
            Size = x.Size,
            // Confirmations = null,
            Fee = x.Fee,
            // HasWitness = null,
            Timestamp = x.Status.BlockTime,
            TransactionId = x.Txid,
            TransactionIndex = null,
            Version = (uint)x.Version,
            VirtualSize = x.Size,
            Weight = x.Weight,
            LockTime = x.Locktime.ToString(),
            Inputs = x.Vin.Select((vin, i) => new QueryTransactionInput
            {
                // CoinBase = null,
                InputAddress = vin.Prevout.ScriptpubkeyAddress,
                InputAmount = vin.Prevout.Value,
                InputIndex = vin.Vout,
                InputTransactionId = vin.Txid,
                WitScript = new WitScript(vin.Witness.Select(s => Encoders.Hex.DecodeData(s)).ToArray()).ToScript()
                        .ToHex(),
                SequenceLock = vin.Sequence.ToString(),
                ScriptSig = vin.Scriptsig,
                ScriptSigAsm = vin.Asm
            }).ToList(),
            Outputs = x.Vout.Select((vout, i) => new QueryTransactionOutput
            {
                Address = vout.ScriptpubkeyAddress,
                Balance = vout.Value,
                Index = i,
                ScriptPubKey = vout.Scriptpubkey,
                OutputType = vout.ScriptpubkeyType,
                ScriptPubKeyAsm = vout.ScriptpubkeyAsm,
                SpentInTransaction = spends?.ElementAtOrDefault(i)?.Txid ?? string.Empty
            }).ToList()
        };
    }

    private async Task PopulateSpentMissingData(List<Outspent> outspents, MempoolTransaction mempoolTransaction, HttpClient client)
    {
        for (int index = 0; index < outspents.Count; index++)
        {
            var outspent = outspents[index];

            if (outspent.Spent && outspent.Txid == null)
            {
                var output = mempoolTransaction.Vout[index];
                if (output != null && !string.IsNullOrEmpty(output.ScriptpubkeyAddress))
                {
                    var txsUrl = $"{MempoolApiRoute}/address/{output.ScriptpubkeyAddress}/txs";

                    var response = await client.GetAsync(txsUrl);
                    _networkService.CheckAndHandleError(response);

                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(response.ReasonPhrase);

                    var trx = await response.Content.ReadFromJsonAsync<List<MempoolTransaction>>(new JsonSerializerOptions()
                    { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

                    bool found = false;
                    foreach (var transaction in trx)
                    {
                        var vinIndex = 0;
                        foreach (var vin in transaction.Vin)
                        {
                            if (vin.Txid == mempoolTransaction.Txid && vin.Vout == index)
                            {
                                outspent.Txid = transaction.Txid;
                                outspent.Vin = vinIndex;

                                found = true;
                                break;
                            }

                            vinIndex++;
                        }

                        if (found) break;
                    }
                }
            }
        }
    }

    public async Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl)
    {
        try
        {
            // fetch block 0 (Genesis Block)
            var blockUrl = $"{MempoolApiRoute}/block-height/0";

            var blockResponse = await GetIndexerClient()
            .GetAsync(blockUrl);

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
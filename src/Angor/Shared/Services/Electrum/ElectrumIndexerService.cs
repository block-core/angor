using System.Text.Json;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services.Electrum;

/// <summary>
/// Electrum-based implementation of IIndexerService.
/// Uses Electrum protocol over SSL/TCP to query blockchain data.
/// </summary>
public class ElectrumIndexerService : IIndexerService
{
    private readonly ILogger<ElectrumIndexerService> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly ElectrumClientPool _clientPool;

    public ElectrumIndexerService(
        ILogger<ElectrumIndexerService> logger,
 INetworkConfiguration networkConfiguration,
  ElectrumClientPool clientPool)
    {
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _clientPool = clientPool;
    }

    public async Task<string> PublishTransactionAsync(string trxHex)
    {
        try
        {
            var client = await _clientPool.GetClientAsync();
            var result = await client.SendRequestAsync<string>(
    "blockchain.transaction.broadcast",
               new object[] { trxHex });

            _logger.LogInformation("Transaction broadcast successful: {TxId}", result);
            return string.Empty; // Success
        }
        catch (ElectrumException ex)
        {
            _logger.LogError(ex, "Failed to broadcast transaction");
            return ex.Message;
        }
    }

    public async Task<AddressBalance[]> GetAdressBalancesAsync(List<AddressInfo> data, bool includeUnconfirmed = false)
    {
        var network = _networkConfiguration.GetNetwork();
        var client = await _clientPool.GetClientAsync();
        var results = new List<AddressBalance>();

        // Process addresses in parallel with batching
        var tasks = data.Select(async addressInfo =>
  {
      try
      {
          var scriptHash = ElectrumScriptHashUtility.AddressToScriptHash(addressInfo.Address, network);
          var balance = await client.SendRequestAsync<ElectrumBalanceResponse>(
          "blockchain.scripthash.get_balance",
         new object[] { scriptHash });

          if (balance.Confirmed > 0 || balance.Unconfirmed != 0)
          {
              return new AddressBalance
              {
                  address = addressInfo.Address,
                  balance = balance.Confirmed,
                  pendingReceived = balance.Unconfirmed > 0 ? balance.Unconfirmed : 0,
                  pendingSent = balance.Unconfirmed < 0 ? Math.Abs(balance.Unconfirmed) : 0
              };
          }
      }
      catch (Exception ex)
      {
          _logger.LogWarning(ex, "Failed to get balance for address {Address}", addressInfo.Address);
      }
      return null;
  });

        var balances = await Task.WhenAll(tasks);
        return balances.Where(b => b != null).Cast<AddressBalance>().ToArray();
    }

    public async Task<List<UtxoData>?> FetchUtxoAsync(string address, int limit, int offset)
    {
        try
        {
            var network = _networkConfiguration.GetNetwork();
            var client = await _clientPool.GetClientAsync();
            var scriptHash = ElectrumScriptHashUtility.AddressToScriptHash(address, network);

            var utxos = await client.SendRequestAsync<List<ElectrumUtxoResponse>>(
                   "blockchain.scripthash.listunspent",
              new object[] { scriptHash });

            if (utxos == null)
                return new List<UtxoData>();

            // Apply offset and limit
            var pagedUtxos = utxos.Skip(offset).Take(limit);

            var utxoDataList = new List<UtxoData>();
            foreach (var utxo in pagedUtxos)
            {
                // Get the transaction to get the scriptPubKey
                var txHex = await GetTransactionHexByIdAsync(utxo.TxHash);
                var tx = network.CreateTransaction(txHex);
                var output = tx.Outputs[utxo.TxPos];

                var data = new UtxoData
                {
                    address = address,
                    scriptHex = output.ScriptPubKey.ToHex(),
                    outpoint = new Outpoint(utxo.TxHash, utxo.TxPos),
                    value = utxo.Value,
                };

                if (utxo.Height > 0)
                {
                    data.blockIndex = utxo.Height;
                }

                utxoDataList.Add(data);
            }

            return utxoDataList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch UTXOs for address {Address}", address);
            throw;
        }
    }

    public async Task<List<QueryTransaction>?> FetchAddressHistoryAsync(string address, string? afterTrxId = null)
    {
        try
        {
            var network = _networkConfiguration.GetNetwork();
            var client = await _clientPool.GetClientAsync();
            var scriptHash = ElectrumScriptHashUtility.AddressToScriptHash(address, network);

            var history = await client.SendRequestAsync<List<ElectrumHistoryItemResponse>>(
                  "blockchain.scripthash.get_history",
                       new object[] { scriptHash });

            if (history == null || !history.Any())
                return new List<QueryTransaction>();

            // Filter transactions after the specified transaction ID
            if (!string.IsNullOrEmpty(afterTrxId))
            {
                var afterIndex = history.FindIndex(h => h.TxHash == afterTrxId);
                if (afterIndex >= 0)
                {
                    history = history.Skip(afterIndex + 1).ToList();
                }
            }

            var transactions = new List<QueryTransaction>();
            foreach (var item in history)
            {
                var txInfo = await GetTransactionInfoByIdAsync(item.TxHash);
                if (txInfo != null)
                {
                    transactions.Add(txInfo);
                }
            }

            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch address history for {Address}", address);
            throw;
        }
    }

    public async Task<FeeEstimations?> GetFeeEstimationAsync(int[] confirmations)
    {
        try
        {
            var client = await _clientPool.GetClientAsync();
            var fees = new List<FeeEstimation>();

            foreach (var blocks in confirmations)
            {
                try
                {
                    // Electrum returns fee in BTC/kB, we need satoshis/kB
                    var feeRate = await client.SendRequestAsync<double>(
                  "blockchain.estimatefee",
                      new object[] { blocks });

                    if (feeRate > 0)
                    {
                        // Convert BTC/kB to satoshis/kB (sat/vB * 1000)
                        var satoshisPerKb = (long)(feeRate * 100_000_000);
                        fees.Add(new FeeEstimation
                        {
                            Confirmations = blocks,
                            FeeRate = satoshisPerKb
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to estimate fee for {Blocks} blocks", blocks);
                }
            }

            return fees.Any() ? new FeeEstimations { Fees = fees } : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get fee estimations");
            return null;
        }
    }

    public async Task<string> GetTransactionHexByIdAsync(string transactionId)
    {
        try
        {
            var client = await _clientPool.GetClientAsync();
            var txHex = await client.SendRequestAsync<string>(
               "blockchain.transaction.get",
                 new object[] { transactionId, false });

            return txHex;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transaction hex for {TxId}", transactionId);
            throw;
        }
    }

    public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
    {
        try
        {
            var client = await _clientPool.GetClientAsync();
            var txJson = await client.SendRequestAsync<JsonElement>(
         "blockchain.transaction.get",
new object[] { transactionId, true });

            return MapElectrumTransactionToQueryTransaction(txJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transaction info for {TxId}", transactionId);
            throw;
        }
    }

    public async Task<IEnumerable<(int index, bool spent)>> GetIsSpentOutputsOnTransactionAsync(string transactionId)
    {
        try
        {
            var network = _networkConfiguration.GetNetwork();
            var client = await _clientPool.GetClientAsync();

            // Get the transaction first to know how many outputs it has
            var txHex = await GetTransactionHexByIdAsync(transactionId);
            var tx = network.CreateTransaction(txHex);

            var results = new List<(int index, bool spent)>();

            for (int i = 0; i < tx.Outputs.Count; i++)
            {
                var output = tx.Outputs[i];
                var scriptHash = ElectrumScriptHashUtility.ScriptToScriptHash(output.ScriptPubKey);

                // Get unspent outputs for this script
                var utxos = await client.SendRequestAsync<List<ElectrumUtxoResponse>>(
      "blockchain.scripthash.listunspent",
        new object[] { scriptHash });

                // Check if this specific output is in the unspent list
                var isUnspent = utxos?.Any(u => u.TxHash == transactionId && u.TxPos == i) ?? false;
                results.Add((i, !isUnspent));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check spent outputs for {TxId}", transactionId);
            throw;
        }
    }

    public async Task<(bool IsOnline, string? GenesisHash)> CheckIndexerNetwork(string indexerUrl)
    {
        try
        {
            // Parse the URL to extract host and port
            if (!TryParseElectrumUrl(indexerUrl, out var host, out var port, out var useSsl))
            {
                _logger.LogWarning("Invalid Electrum URL format: {Url}", indexerUrl);
                return (false, null);
            }

            var config = new ElectrumServerConfig
            {
                Host = host,
                Port = port,
                UseSsl = useSsl,
                Timeout = TimeSpan.FromSeconds(10)
            };

            var client = await _clientPool.GetClientForServerAsync(config);

            // Get the block header at height 0 (genesis block)
            var genesisHeader = await client.SendRequestAsync<string>(
    "blockchain.block.header",
                new object[] { 0 });

            // The block hash is the double SHA256 of the header, but Electrum returns it directly
            // We need to get the block hash separately
            var blockHash = await GetBlockHashAtHeight(client, 0);

            return (true, blockHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Electrum server network: {Url}", indexerUrl);
            return (false, null);
        }
    }

    public bool ValidateGenesisBlockHash(string fetchedHash, string expectedHash)
    {
        return fetchedHash.StartsWith(expectedHash, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(fetchedHash);
    }

    #region Private Helper Methods

    private async Task<string?> GetBlockHashAtHeight(ElectrumClient client, int height)
    {
        try
        {
            // Get the block header at the specified height
            var header = await client.SendRequestAsync<string>(
          "blockchain.block.header",
                        new object[] { height });

            if (string.IsNullOrEmpty(header))
                return null;

            // The block hash is calculated by double SHA256 of the header
            var headerBytes = Encoders.Hex.DecodeData(header);
            var hash1 = System.Security.Cryptography.SHA256.HashData(headerBytes);
            var hash2 = System.Security.Cryptography.SHA256.HashData(hash1);
            Array.Reverse(hash2); // Bitcoin uses little-endian
            return Encoders.Hex.EncodeData(hash2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get block hash at height {Height}", height);
            return null;
        }
    }

    private static bool TryParseElectrumUrl(string url, out string host, out int port, out bool useSsl)
    {
        host = string.Empty;
        port = 50002;
        useSsl = true;

        try
        {
            // Handle formats like:
            // - ssl://host:port
            // - tcp://host:port
            // - host:port (defaults to SSL)
            // - electrum://host:port

            if (url.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            {
                useSsl = false;
                url = url[6..];
            }
            else if (url.StartsWith("ssl://", StringComparison.OrdinalIgnoreCase))
            {
                useSsl = true;
                url = url[6..];
            }
            else if (url.StartsWith("electrum://", StringComparison.OrdinalIgnoreCase))
            {
                url = url[11..];
            }

            var parts = url.Split(':');
            if (parts.Length >= 1)
            {
                host = parts[0];
                if (parts.Length >= 2 && int.TryParse(parts[1], out var parsedPort))
                {
                    port = parsedPort;
                }
                return !string.IsNullOrEmpty(host);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private QueryTransaction? MapElectrumTransactionToQueryTransaction(JsonElement txJson)
    {
        try
        {
            var txId = txJson.GetProperty("txid").GetString();
            var version = txJson.GetProperty("version").GetUInt32();
            var locktime = txJson.GetProperty("locktime").GetInt64();
            var size = txJson.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt32() : 0;
            var vsize = txJson.TryGetProperty("vsize", out var vsizeEl) ? vsizeEl.GetInt32() : size;
            var weight = txJson.TryGetProperty("weight", out var weightEl) ? weightEl.GetInt32() : vsize * 4;

            string? blockHash = null;
            long? blockHeight = null;
            long timestamp = 0;

            if (txJson.TryGetProperty("blockhash", out var blockHashEl))
            {
                blockHash = blockHashEl.GetString();
            }

            if (txJson.TryGetProperty("blocktime", out var blockTimeEl))
            {
                timestamp = blockTimeEl.GetInt64();
            }

            // Parse inputs
            var inputs = new List<QueryTransactionInput>();
            if (txJson.TryGetProperty("vin", out var vinArray))
            {
                foreach (var vin in vinArray.EnumerateArray())
                {
                    var input = new QueryTransactionInput
                    {
                        InputTransactionId = vin.TryGetProperty("txid", out var txidEl) ? txidEl.GetString() : null,
                        InputIndex = vin.TryGetProperty("vout", out var voutEl) ? voutEl.GetInt32() : 0,
                        SequenceLock = vin.TryGetProperty("sequence", out var seqEl) ? seqEl.GetInt64().ToString() : null,
                        ScriptSig = vin.TryGetProperty("scriptSig", out var scriptSigEl) && scriptSigEl.TryGetProperty("hex", out var hexEl)
                     ? hexEl.GetString() : null,
                    };

                    if (vin.TryGetProperty("txinwitness", out var witnessEl))
                    {
                        var witnessData = witnessEl.EnumerateArray()
                           .Select(w => Encoders.Hex.DecodeData(w.GetString() ?? ""))
                            .ToArray();
                        input.WitScript = new WitScript(witnessData).ToScript().ToHex();
                    }

                    inputs.Add(input);
                }
            }

            // Parse outputs
            var outputs = new List<QueryTransactionOutput>();
            if (txJson.TryGetProperty("vout", out var voutArray))
            {
                var index = 0;
                foreach (var vout in voutArray.EnumerateArray())
                {
                    var output = new QueryTransactionOutput
                    {
                        Index = index++,
                        Balance = (long)(vout.GetProperty("value").GetDouble() * 100_000_000),
                    };

                    if (vout.TryGetProperty("scriptPubKey", out var scriptPubKey))
                    {
                        output.ScriptPubKey = scriptPubKey.TryGetProperty("hex", out var spkHex) ? spkHex.GetString() : null;
                        output.ScriptPubKeyAsm = scriptPubKey.TryGetProperty("asm", out var spkAsm) ? spkAsm.GetString() : null;
                        output.OutputType = scriptPubKey.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                        if (scriptPubKey.TryGetProperty("address", out var addrEl))
                        {
                            output.Address = addrEl.GetString();
                        }
                        else if (scriptPubKey.TryGetProperty("addresses", out var addrsEl) && addrsEl.GetArrayLength() > 0)
                        {
                            output.Address = addrsEl[0].GetString();
                        }
                    }

                    outputs.Add(output);
                }
            }

            return new QueryTransaction
            {
                TransactionId = txId,
                Version = version,
                LockTime = locktime.ToString(),
                Size = size,
                VirtualSize = vsize,
                Weight = weight,
                BlockHash = blockHash,
                BlockIndex = blockHeight,
                Timestamp = timestamp,
                Inputs = inputs,
                Outputs = outputs
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map Electrum transaction to QueryTransaction");
            return null;
        }
    }

    #endregion
}

#region Electrum Response Models

/// <summary>
/// Response from blockchain.scripthash.get_balance
/// </summary>
internal class ElectrumBalanceResponse
{
    public long Confirmed { get; set; }
    public long Unconfirmed { get; set; }
}

/// <summary>
/// Response item from blockchain.scripthash.listunspent
/// </summary>
internal class ElectrumUtxoResponse
{
    public int Height { get; set; }
    public string TxHash { get; set; } = string.Empty;
    public int TxPos { get; set; }
    public long Value { get; set; }
}

/// <summary>
/// Response item from blockchain.scripthash.get_history
/// </summary>
internal class ElectrumHistoryItemResponse
{
    public int Height { get; set; }
    public string TxHash { get; set; } = string.Empty;
    public long Fee { get; set; }
}

#endregion

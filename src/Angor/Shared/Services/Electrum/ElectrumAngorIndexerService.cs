using System.Text.Json;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services.Electrum;

/// <summary>
/// Electrum-based implementation of IAngorIndexerService.
/// Provides Angor-specific project and investment data by querying blockchain via Electrum protocol.
/// </summary>
public class ElectrumAngorIndexerService : IAngorIndexerService
{
    private readonly ILogger<ElectrumAngorIndexerService> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IDerivationOperations _derivationOperations;
    private readonly ElectrumClientPool _clientPool;
    private readonly MempoolIndexerMappers _mappers;

    public bool ReadFromAngorApi { get; set; } = false;

    public ElectrumAngorIndexerService(
        ILogger<ElectrumAngorIndexerService> logger,
        INetworkConfiguration networkConfiguration,
        IDerivationOperations derivationOperations,
        ElectrumClientPool clientPool,
        MempoolIndexerMappers mappers)
    {
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _derivationOperations = derivationOperations;
        _clientPool = clientPool;
        _mappers = mappers;
    }

    public Task<List<ProjectIndexerData>> GetProjectsAsync(int? offset, int limit)
    {
        // GetProjectsAsync is not supported via Electrum protocol
        // This requires a centralized Angor indexer that maintains a list of all projects
        _logger.LogWarning("GetProjectsAsync is not supported via Electrum protocol. Use Angor API indexer instead.");
        return Task.FromResult(new List<ProjectIndexerData>());
    }

    public async Task<ProjectIndexerData?> GetProjectByIdAsync(string projectId)
    {
        if (string.IsNullOrEmpty(projectId) || projectId.Length <= 1)
        {
            return null;
        }

        try
        {
            // Convert project ID (Angor key) to Bitcoin address
            var projectAddress = _derivationOperations.ConvertAngorKeyToBitcoinAddress(projectId);

            // Get all transactions for this address via Electrum
            var transactions = await GetAddressTransactionsAsync(projectAddress);

            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("No transactions found for project {ProjectId}", projectId);
                return null;
            }

            // Convert to mempool format for compatibility with existing mapper
            var mempoolTxs = transactions.Select(ConvertToMempoolTransaction).ToList();

            // Use the existing mapper
            return _mappers.ConvertTransactionsToProjectIndexerData(projectId, mempoolTxs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching project by ID {ProjectId}", projectId);
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
            // Convert project ID to Bitcoin address
            var projectAddress = _derivationOperations.ConvertAngorKeyToBitcoinAddress(projectId);

            // Get all transactions for this address
            var transactions = await GetAddressTransactionsAsync(projectAddress);

            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("No transactions found for project {ProjectId}", projectId);
                return (projectId, null);
            }

            // Convert to mempool format for compatibility with existing mapper
            var mempoolTxs = transactions.Select(ConvertToMempoolTransaction).ToList();

            // Use the existing mapper
            var stats = _mappers.CalculateProjectStats(projectId, mempoolTxs);

            return (projectId, stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching project stats for {ProjectId}", projectId);
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
            // Convert project ID to Bitcoin address
            var projectAddress = _derivationOperations.ConvertAngorKeyToBitcoinAddress(projectId);

            // Get all transactions for this address
            var transactions = await GetAddressTransactionsAsync(projectAddress);

            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("No transactions found for project {ProjectId}", projectId);
                return new List<ProjectInvestment>();
            }

            // Convert to mempool format for compatibility with existing mapper
            var mempoolTxs = transactions.Select(ConvertToMempoolTransaction).ToList();

            // Use the existing mapper
            return _mappers.ConvertTransactionsToInvestments(projectId, mempoolTxs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching investments for project {ProjectId}", projectId);
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
                _logger.LogWarning("No investment found for project {ProjectId} and investor {InvestorPubKey}",
                        projectId, investorPubKey);
            }

            return investment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching investment for project {ProjectId} and investor {InvestorPubKey}",
             projectId, investorPubKey);
            return null;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets all transactions for an address via Electrum protocol.
    /// </summary>
    private async Task<List<AngorElectrumTransactionInfo>?> GetAddressTransactionsAsync(string address)
    {
        try
        {
            var network = _networkConfiguration.GetNetwork();
            var client = await _clientPool.GetClientAsync();
            var scriptHash = ElectrumScriptHashUtility.AddressToScriptHash(address, network);

            // Get transaction history for the address
            var history = await client.SendRequestAsync<List<AngorElectrumHistoryItem>>(
                 "blockchain.scripthash.get_history",
                new object[] { scriptHash });

            if (history == null || !history.Any())
                return new List<AngorElectrumTransactionInfo>();

            // Fetch full transaction details for each transaction
            var transactions = new List<AngorElectrumTransactionInfo>();
            foreach (var item in history)
            {
                try
                {
                    var txJson = await client.SendRequestAsync<JsonElement>(
                        "blockchain.transaction.get",
                           new object[] { item.TxHash, true });

                    var txInfo = ParseElectrumTransaction(txJson, item.Height);
                    if (txInfo != null)
                    {
                        transactions.Add(txInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch transaction {TxHash}", item.TxHash);
                }
            }

            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transactions for address {Address}", address);
            return null;
        }
    }

    /// <summary>
    /// Parses Electrum JSON response to internal transaction info model.
    /// </summary>
    private AngorElectrumTransactionInfo? ParseElectrumTransaction(JsonElement txJson, int blockHeight)
    {
        try
        {
            var txInfo = new AngorElectrumTransactionInfo
            {
                Txid = txJson.GetProperty("txid").GetString() ?? string.Empty,
                Version = (int)txJson.GetProperty("version").GetUInt32(),
                Locktime = (int)txJson.GetProperty("locktime").GetInt64(),
                Size = txJson.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt32() : 0,
                Weight = txJson.TryGetProperty("weight", out var weightEl) ? weightEl.GetInt32() : 0,
                Status = new AngorElectrumTxStatus
                {
                    Confirmed = blockHeight > 0,
                    BlockHeight = blockHeight,
                    BlockHash = txJson.TryGetProperty("blockhash", out var bhEl) ? bhEl.GetString() : null,
                    BlockTime = txJson.TryGetProperty("blocktime", out var btEl) ? btEl.GetInt64() : 0
                }
            };

            // Parse inputs
            if (txJson.TryGetProperty("vin", out var vinArray))
            {
                foreach (var vin in vinArray.EnumerateArray())
                {
                    var input = new AngorElectrumVin
                    {
                        Txid = vin.TryGetProperty("txid", out var txidEl) ? txidEl.GetString() : null,
                        Vout = vin.TryGetProperty("vout", out var voutEl) ? voutEl.GetInt32() : 0,
                        Sequence = vin.TryGetProperty("sequence", out var seqEl) ? seqEl.GetInt64() : 0,
                    };

                    if (vin.TryGetProperty("scriptSig", out var scriptSigEl))
                    {
                        input.Scriptsig = scriptSigEl.TryGetProperty("hex", out var hexEl) ? hexEl.GetString() : null;
                        input.Asm = scriptSigEl.TryGetProperty("asm", out var asmEl) ? asmEl.GetString() : null;
                    }

                    if (vin.TryGetProperty("txinwitness", out var witnessEl))
                    {
                        input.Witness = witnessEl.EnumerateArray()
                        .Select(w => w.GetString() ?? string.Empty)
                               .ToList();
                    }

                    // Try to get prevout info if available
                    if (vin.TryGetProperty("prevout", out var prevoutEl))
                    {
                        input.Prevout = new AngorElectrumPrevOut
                        {
                            Value = (long)(prevoutEl.GetProperty("value").GetDouble() * 100_000_000),
                            Scriptpubkey = prevoutEl.TryGetProperty("scriptPubKey", out var spk)
                      ? (spk.TryGetProperty("hex", out var spkHex) ? spkHex.GetString() : null)
                                : null
                        };

                        if (prevoutEl.TryGetProperty("scriptPubKey", out var spkEl))
                        {
                            if (spkEl.TryGetProperty("address", out var addrEl))
                            {
                                input.Prevout.ScriptpubkeyAddress = addrEl.GetString();
                            }
                            if (spkEl.TryGetProperty("type", out var typeEl))
                            {
                                input.Prevout.ScriptpubkeyType = typeEl.GetString();
                            }
                        }
                    }

                    txInfo.Vin.Add(input);
                }
            }

            // Parse outputs
            if (txJson.TryGetProperty("vout", out var voutArray))
            {
                foreach (var vout in voutArray.EnumerateArray())
                {
                    var output = new AngorElectrumPrevOut
                    {
                        Value = (long)(vout.GetProperty("value").GetDouble() * 100_000_000)
                    };

                    if (vout.TryGetProperty("scriptPubKey", out var scriptPubKey))
                    {
                        output.Scriptpubkey = scriptPubKey.TryGetProperty("hex", out var spkHex) ? spkHex.GetString() : null;
                        output.ScriptpubkeyAsm = scriptPubKey.TryGetProperty("asm", out var spkAsm) ? spkAsm.GetString() : null;
                        output.ScriptpubkeyType = scriptPubKey.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                        if (scriptPubKey.TryGetProperty("address", out var addrEl))
                        {
                            output.ScriptpubkeyAddress = addrEl.GetString();
                        }
                        else if (scriptPubKey.TryGetProperty("addresses", out var addrsEl) && addrsEl.GetArrayLength() > 0)
                        {
                            output.ScriptpubkeyAddress = addrsEl[0].GetString();
                        }
                    }

                    txInfo.Vout.Add(output);
                }
            }

            return txInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Electrum transaction");
            return null;
        }
    }

    /// <summary>
    /// Converts internal transaction info to MempoolSpaceIndexerApi.MempoolTransaction for mapper compatibility.
    /// </summary>
    private MempoolSpaceIndexerApi.MempoolTransaction ConvertToMempoolTransaction(AngorElectrumTransactionInfo tx)
    {
        return new MempoolSpaceIndexerApi.MempoolTransaction
        {
            Txid = tx.Txid,
            Version = tx.Version,
            Locktime = tx.Locktime,
            Size = tx.Size,
            Weight = tx.Weight,
            Fee = tx.Fee,
            Status = new MempoolSpaceIndexerApi.UtxoStatus
            {
                Confirmed = tx.Status.Confirmed,
                BlockHeight = tx.Status.BlockHeight,
                BlockHash = tx.Status.BlockHash ?? string.Empty,
                BlockTime = tx.Status.BlockTime
            },
            Vin = tx.Vin.Select(vin => new MempoolSpaceIndexerApi.Vin
            {
                Txid = vin.Txid ?? string.Empty,
                Vout = vin.Vout,
                Scriptsig = vin.Scriptsig ?? string.Empty,
                Asm = vin.Asm ?? string.Empty,
                Sequence = vin.Sequence,
                Witness = vin.Witness,
                Prevout = vin.Prevout != null ? new MempoolSpaceIndexerApi.PrevOut
                {
                    Value = vin.Prevout.Value,
                    Scriptpubkey = vin.Prevout.Scriptpubkey ?? string.Empty,
                    ScriptpubkeyAddress = vin.Prevout.ScriptpubkeyAddress ?? string.Empty,
                    ScriptpubkeyAsm = vin.Prevout.ScriptpubkeyAsm ?? string.Empty,
                    ScriptpubkeyType = vin.Prevout.ScriptpubkeyType ?? string.Empty
                } : new MempoolSpaceIndexerApi.PrevOut()
            }).ToList(),
            Vout = tx.Vout.Select(vout => new MempoolSpaceIndexerApi.PrevOut
            {
                Value = vout.Value,
                Scriptpubkey = vout.Scriptpubkey ?? string.Empty,
                ScriptpubkeyAddress = vout.ScriptpubkeyAddress ?? string.Empty,
                ScriptpubkeyAsm = vout.ScriptpubkeyAsm ?? string.Empty,
                ScriptpubkeyType = vout.ScriptpubkeyType ?? string.Empty
            }).ToList()
        };
    }

    #endregion
}

#region Internal Models for Angor Electrum Service

/// <summary>
/// Internal model for Electrum transaction history item.
/// </summary>
internal class AngorElectrumHistoryItem
{
    public int Height { get; set; }
    public string TxHash { get; set; } = string.Empty;
    public long Fee { get; set; }
}

/// <summary>
/// Internal model for parsed Electrum transaction.
/// </summary>
internal class AngorElectrumTransactionInfo
{
    public string Txid { get; set; } = string.Empty;
    public int Version { get; set; }
    public int Locktime { get; set; }
    public int Size { get; set; }
    public int Weight { get; set; }
    public int Fee { get; set; }
    public AngorElectrumTxStatus Status { get; set; } = new();
    public List<AngorElectrumVin> Vin { get; set; } = new();
    public List<AngorElectrumPrevOut> Vout { get; set; } = new();
}

/// <summary>
/// Transaction status information.
/// </summary>
internal class AngorElectrumTxStatus
{
    public bool Confirmed { get; set; }
    public int BlockHeight { get; set; }
    public string? BlockHash { get; set; }
    public long BlockTime { get; set; }
}

/// <summary>
/// Transaction input.
/// </summary>
internal class AngorElectrumVin
{
    public bool IsCoinbase { get; set; }
    public AngorElectrumPrevOut? Prevout { get; set; }
    public string? Scriptsig { get; set; }
    public string? Asm { get; set; }
    public long Sequence { get; set; }
    public string? Txid { get; set; }
    public int Vout { get; set; }
    public List<string> Witness { get; set; } = new();
}

/// <summary>
/// Previous output / current output.
/// </summary>
internal class AngorElectrumPrevOut
{
    public long Value { get; set; }
    public string? Scriptpubkey { get; set; }
    public string? ScriptpubkeyAddress { get; set; }
    public string? ScriptpubkeyAsm { get; set; }
    public string? ScriptpubkeyType { get; set; }
}

#endregion

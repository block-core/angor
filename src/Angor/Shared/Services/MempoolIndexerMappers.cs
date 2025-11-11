using Angor.Shared.Models;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services;

public class MempoolIndexerMappers
{
    private readonly ILogger<MempoolIndexerMappers> _logger;

    public MempoolIndexerMappers(ILogger<MempoolIndexerMappers> logger)
    {
        _logger = logger;
    }

    public ProjectIndexerData? ConvertTransactionsToProjectIndexerData(string projectId, List<MempoolSpaceIndexerApi.MempoolTransaction> trxs)
    {
        try
        {
            // Step 1: Find the funding transaction (first transaction with founder key and event ID in OP_RETURN)
            MempoolSpaceIndexerApi.MempoolTransaction? fundingTrx = null;
            string? founderKey = null;
            string? nostrEventId = null;

            // Sort transactions by block height to process them in order
            var sortedTransactions = trxs.OrderBy(t => t.Status.BlockHeight).ToList();

            foreach (var trx in sortedTransactions)
            {
                // Check if this transaction has at least 2 outputs
                if (trx.Vout.Count < 2)
                    continue;

                // Second output should be OP_RETURN with founder info
                var opReturnOutput = trx.Vout[1];

                if (opReturnOutput.ScriptpubkeyType == "op_return" || opReturnOutput.ScriptpubkeyType == "nulldata")
                {
                    try
                    {
                        var parsedData = ParseFounderInfoFromOpReturn(opReturnOutput.Scriptpubkey);
                        if (parsedData != null)
                        {
                            fundingTrx = trx;
                            founderKey = parsedData.Value.founderKey;
                            nostrEventId = parsedData.Value.nostrEventId;
                            _logger.LogInformation($"Found funding transaction {trx.Txid} for project {projectId}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"Failed to parse founder info from transaction {trx.Txid}");
                    }
                }
            }

            if (fundingTrx == null || string.IsNullOrEmpty(founderKey))
            {
                _logger.LogWarning($"No funding transaction found for project {projectId}");
                return null;
            }

            // Step 2: Count investment transactions (transactions after the funding transaction with investor key in OP_RETURN)
            long totalInvestmentsCount = 0;

            foreach (var trx in sortedTransactions)
            {
                // Skip the funding transaction
                if (trx.Txid == fundingTrx.Txid)
                    continue;

                // Check if this transaction has at least 2 outputs
                if (trx.Vout.Count < 2)
                    continue;

                // Second output should be OP_RETURN with investor info
                var opReturnOutput = trx.Vout[1];

                if (opReturnOutput.ScriptpubkeyType == "op_return" || opReturnOutput.ScriptpubkeyType == "nulldata")
                {
                    try
                    {
                        var investorKey = ParseInvestorInfoFromOpReturn(opReturnOutput.Scriptpubkey);
                        if (!string.IsNullOrEmpty(investorKey))
                        {
                            totalInvestmentsCount++;
                            _logger.LogDebug($"Found investment transaction {trx.Txid} for project {projectId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"Failed to parse investor info from transaction {trx.Txid}");
                    }
                }
            }

            // Step 3: Create ProjectIndexerData
            var projectIndexerData = new ProjectIndexerData
            {
                FounderKey = founderKey,
                ProjectIdentifier = projectId,
                CreatedOnBlock = fundingTrx.Status.BlockHeight,
                NostrEventId = nostrEventId ?? string.Empty,
                TrxId = fundingTrx.Txid,
                TotalInvestmentsCount = totalInvestmentsCount
            };

            _logger.LogInformation($"Successfully converted transactions to ProjectIndexerData for project {projectId}. " +
                    $"Founder: {founderKey}, EventId: {nostrEventId}, Investments: {totalInvestmentsCount}");

            return projectIndexerData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error converting transactions to ProjectIndexerData for project {projectId}");
            return null;
        }
    }

    public (string founderKey, string nostrEventId)? ParseFounderInfoFromOpReturn(string scriptPubKeyHex)
    {
        try
        {
            var scriptBytes = Encoders.Hex.DecodeData(scriptPubKeyHex);
            var script = new Blockcore.Consensus.ScriptInfo.Script(scriptBytes);

            if (!script.IsUnspendable)
                return null;

            var ops = script.ToOps().ToList();

            // Expected format for founder transaction: OP_RETURN <founder_pubkey> <key_type> <nostr_event_id>
            // ops[0] = OP_RETURN
            // ops[1] = founder public key (33 bytes)
            // ops[2] = key type (2 bytes)
            // ops[3] = nostr event id (32 bytes hex)

            if (ops.Count >= 4)
            {
                var founderKeyBytes = ops[1].PushData;
                var nostrEventIdBytes = ops[3].PushData;

                if (founderKeyBytes != null && founderKeyBytes.Length == 33)
                {
                    var founderKey = Encoders.Hex.EncodeData(founderKeyBytes);
                    var nostrEventId = nostrEventIdBytes != null ? Encoders.Hex.EncodeData(nostrEventIdBytes) : string.Empty;

                    return (founderKey, nostrEventId);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse founder info from OP_RETURN");
            return null;
        }
    }

    public string? ParseInvestorInfoFromOpReturn(string scriptPubKeyHex)
    {
        try
        {
            var scriptBytes = Encoders.Hex.DecodeData(scriptPubKeyHex);
            var script = new Blockcore.Consensus.ScriptInfo.Script(scriptBytes);

            if (!script.IsUnspendable)
                return null;

            var ops = script.ToOps().ToList();

            // Expected format for investor transaction: OP_RETURN <investor_pubkey> [<secret_hash>]
            // ops[0] = OP_RETURN
            // ops[1] = investor public key (33 bytes)
            // ops[2] = optional secret hash (32 bytes)

            if (ops.Count >= 2)
            {
                var investorKeyBytes = ops[1].PushData;

                if (investorKeyBytes != null && investorKeyBytes.Length == 33)
                {
                    return Encoders.Hex.EncodeData(investorKeyBytes);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse investor info from OP_RETURN");
            return null;
        }
    }

    /// <summary>
    /// Calculates project statistics from a list of transactions.
    /// Analyzes investment transactions to determine investor count and total amount invested.
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <param name="trxs">List of transactions for the project</param>
    /// <returns>ProjectStats with calculated values or null if calculation fails</returns>
    public ProjectStats? CalculateProjectStats(string projectId, List<MempoolSpaceIndexerApi.MempoolTransaction> trxs)
    {
        try
        {
            // Sort transactions by block height to process them in order
            var sortedTransactions = trxs.OrderBy(t => t.Status.BlockHeight).ToList();

            // Step 1: Find the funding transaction
            MempoolSpaceIndexerApi.MempoolTransaction? fundingTrx = null;
            foreach (var trx in sortedTransactions)
            {
                if (trx.Vout.Count < 2)
                    continue;

                var opReturnOutput = trx.Vout[1];
                if (opReturnOutput.ScriptpubkeyType == "op_return" || opReturnOutput.ScriptpubkeyType == "nulldata")
                {
                    var parsedData = ParseFounderInfoFromOpReturn(opReturnOutput.Scriptpubkey);
                    if (parsedData != null)
                    {
                        fundingTrx = trx;
                        _logger.LogDebug($"Found funding transaction {trx.Txid} for stats calculation");
                        break;
                    }
                }
            }

            if (fundingTrx == null)
            {
                _logger.LogWarning($"No funding transaction found for project {projectId} in stats calculation");
                return null;
            }

            // Step 2: Analyze investment transactions
            var uniqueInvestors = new HashSet<string>();
            long totalAmountInvested = 0;

            foreach (var trx in sortedTransactions)
            {
                // Skip the funding transaction
                if (trx.Txid == fundingTrx.Txid)
                    continue;

                // Check if this transaction has at least 2 outputs
                if (trx.Vout.Count < 2)
                    continue;

                // Check if second output is OP_RETURN with investor info
                var opReturnOutput = trx.Vout[1];
                if (opReturnOutput.ScriptpubkeyType == "op_return" || opReturnOutput.ScriptpubkeyType == "nulldata")
                {
                    try
                    {
                        var investorKey = ParseInvestorInfoFromOpReturn(opReturnOutput.Scriptpubkey);
                        if (!string.IsNullOrEmpty(investorKey))
                        {
                            // Add investor to unique set
                            uniqueInvestors.Add(investorKey);

                            // Sum up all v1_p2tr (taproot) outputs which are the investment stage outputs
                            // Skip first two outputs (Angor fee and OP_RETURN)
                            long investmentAmount = 0;
                            for (int i = 2; i < trx.Vout.Count; i++)
                            {
                                var vout = trx.Vout[i];

                                // Check if this is a taproot output (investment stage output)
                                if (vout.ScriptpubkeyType == "v1_p2tr")
                                {
                                    investmentAmount += vout.Value;
                                    _logger.LogTrace($"Found taproot output at index {i} with value {vout.Value} in transaction {trx.Txid}");
                                }
                            }

                            if (investmentAmount > 0)
                            {
                                totalAmountInvested += investmentAmount;
                                _logger.LogDebug($"Investment transaction {trx.Txid}: investor={investorKey}, amount={investmentAmount}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"Failed to parse investment info from transaction {trx.Txid}");
                    }
                }
            }

            var stats = new ProjectStats
            {
                InvestorCount = uniqueInvestors.Count,
                AmountInvested = totalAmountInvested,

                // TODO: Implement penalty calculations
                // To calculate AmountInPenalties and CountInPenalties, we need to:
                // 1. For each investment transaction, track the taproot outputs (v1_p2tr)
                // 2. Fetch spending transactions for these outputs
                // 3. Analyze the witness script (witscript) of the spending transaction:
                //    - Spent by Founder: Has OP_CLTV and 3 witness blocks
                //    - Spent by Investor to Penalty: Has OP_CHECKSIGVERIFY, OP_CHECKSIG and 4 witness blocks
                //    - Spent by Investor (End of Project): Has OP_CLTV and 3 witness blocks (different timelock)
                // 4. Sum up amounts and count transactions where investor spent to penalty
                AmountInPenalties = 0,
                CountInPenalties = 0
            };

            _logger.LogInformation($"Calculated stats for project {projectId}: " +
                $"Investors={stats.InvestorCount}, AmountInvested={stats.AmountInvested}");

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calculating project stats for project {projectId}");
            return null;
        }
    }
}

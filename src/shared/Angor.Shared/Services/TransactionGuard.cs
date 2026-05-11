using NBitcoin;

namespace Angor.Shared.Services;

/// <summary>
/// Pre-broadcast safety checks for transactions.
/// </summary>
public static class TransactionGuard
{
    /// <summary>
    /// Rejects any transaction containing a P2TR (SegWit v1) output whose
    /// 32-byte witness program is all zeros. Such outputs are unspendable
    /// and indicate a bug in taproot key derivation (e.g. the .NET 10
    /// ARM64 JIT span-aliasing bug in TaprootFullPubKey.ComputeTapTweak).
    /// </summary>
    /// <param name="trxHex">Raw transaction hex.</param>
    /// <returns>Error message if rejected; null if the transaction is safe to broadcast.</returns>
    public static string? RejectAllZeroP2trOutputs(string trxHex)
    {
        if (string.IsNullOrWhiteSpace(trxHex))
            return null; // let downstream handle empty input

        Transaction tx;
        try
        {
            tx = Transaction.Parse(trxHex, Network.Main);
        }
        catch
        {
            // If we can't parse on mainnet, try other networks; but the
            // output script check is network-independent so any will do.
            try
            {
                tx = Transaction.Parse(trxHex, Network.TestNet);
            }
            catch
            {
                return null; // unparseable hex — let the indexer reject it
            }
        }

        for (int i = 0; i < tx.Outputs.Count; i++)
        {
            var script = tx.Outputs[i].ScriptPubKey;
            var ops = script.ToOps().ToArray();

            // P2TR: OP_1 <32 bytes>
            if (ops.Length == 2 &&
                ops[0].Code == OpcodeType.OP_1 &&
                ops[1].PushData?.Length == 32)
            {
                byte[] key = ops[1].PushData;
                bool allZero = true;
                for (int j = 0; j < 32; j++)
                {
                    if (key[j] != 0)
                    {
                        allZero = false;
                        break;
                    }
                }

                if (allZero)
                {
                    return $"BLOCKED: Transaction output {i} has an all-zero P2TR witness program " +
                           $"(unspendable address). This indicates a taproot key derivation bug. " +
                           $"TxId: {tx.GetHash()}";
                }
            }
        }

        return null;
    }
}

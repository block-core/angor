using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Utilities;

public static class TransactionExtension
{
    public static bool IsTaprooOutput(this IndexedTxOut txout)
    {
        return txout.TxOut.ScriptPubKey.IsTaprooOutput();
    }

    public static bool IsTaprooOutput(this Script script)
    {
        var _script = script.ToBytes();
        return script.Length == 34 && _script[0] == 0x51 && _script[1] == 32;
    }

    public static long GetTotalInvestmentAmount(this Transaction investmentTransaction)
    {
        return investmentTransaction.Outputs.AsIndexedOutputs()
                .Where(txout => txout.IsTaprooOutput())
            .Sum(output => output.TxOut.Value.Satoshi);
    }

    public static long GetTotalInvestmentAmount(this NBitcoin.Transaction investmentTransaction)
    {
        return investmentTransaction.Outputs.AsIndexedOutputs()
         .Where(output => output.TxOut.ScriptPubKey.IsScriptType(NBitcoin.ScriptType.Taproot))
                .Sum(output => output.TxOut.Value.Satoshi);
    }
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Utilities;

public static class TransactionExtension
{
    public static bool IsTaprooOutput(this Blockcore.Consensus.TransactionInfo.IndexedTxOut txout)
    {
        return txout.TxOut.ScriptPubKey.IsTaprooOutput();
    }

    public static bool IsTaprooOutput(this Blockcore.Consensus.ScriptInfo.Script script)
    {
        var _script = script.ToBytes();
        return script.Length == 34 && _script[0] == 0x51 && _script[1] == 32;
    }

    public static long GetTotalInvestmentAmount(this Blockcore.Consensus.TransactionInfo.Transaction investmentTransaction)
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

    /// <summary>
    /// Converts an NBitcoin Transaction to a QueryTransaction object for JSON serialization
    /// Note: Size and Weight values are approximations based on VirtualSize
    /// </summary>
    public static QueryTransaction ToQueryTransaction(this NBitcoin.Transaction transaction, NBitcoin.Network network)
    {
        var virtualSize = (int)transaction.GetVirtualSize();
        
        return new QueryTransaction
        {
            TransactionId = transaction.GetHash().ToString(),
            Version = transaction.Version,
            LockTime = transaction.LockTime.ToString(),
            Size = virtualSize,
            VirtualSize = virtualSize,
            Weight = virtualSize * 4, // Approximation: actual weight may differ for witness transactions
            HasWitness = transaction.HasWitness,
            Inputs = transaction.Inputs.AsIndexedInputs().Select(input => new QueryTransactionInput
            {
                InputIndex = (int)input.Index,
                InputTransactionId = input.PrevOut.Hash.ToString(),
                InputAddress = input.TxIn.ScriptSig.GetSignerAddress(network)?.ToString() ?? string.Empty,
                ScriptSig = input.TxIn.ScriptSig.ToHex(),
                ScriptSigAsm = input.TxIn.ScriptSig.ToString(),
                WitScript = input.TxIn.WitScript?.ToString() ?? string.Empty,
                SequenceLock = input.TxIn.Sequence.ToString()
            }).ToList(),
            Outputs = transaction.Outputs.AsIndexedOutputs().Select(output => new QueryTransactionOutput
            {
                Index = (int)output.N,
                Address = output.TxOut.ScriptPubKey.GetDestinationAddress(network)?.ToString() ?? string.Empty,
                Balance = output.TxOut.Value.Satoshi,
                ScriptPubKey = output.TxOut.ScriptPubKey.ToHex(),
                ScriptPubKeyAsm = output.TxOut.ScriptPubKey.ToString(),
                OutputType = GetScriptTypeNBitcoin(output.TxOut.ScriptPubKey)
            }).ToList()
        };
    }

    /// <summary>
    /// Converts a Blockcore Transaction to a QueryTransaction object for JSON serialization
    /// Note: InputAddress is not populated as Blockcore doesn't provide GetSignerAddress.
    /// Size and Weight values are approximations based on VirtualSize.
    /// </summary>
    public static QueryTransaction ToQueryTransaction(this Blockcore.Consensus.TransactionInfo.Transaction transaction, Blockcore.Networks.Network network)
    {
        var virtualSize = (int)transaction.GetVirtualSize(4);
        return new QueryTransaction
        {
            TransactionId = transaction.GetHash().ToString(),
            Version = transaction.Version,
            LockTime = transaction.LockTime.ToString(),
            Size = virtualSize,
            VirtualSize = virtualSize,
            Weight = virtualSize * 4, // Approximation: actual weight may differ for witness transactions
            HasWitness = transaction.HasWitness,
            Inputs = transaction.Inputs.AsIndexedInputs().Select(input => new QueryTransactionInput
            {
                InputIndex = (int)input.Index,
                InputTransactionId = input.PrevOut.Hash.ToString(),
                InputAddress = string.Empty, // Blockcore doesn't have GetSignerAddress
                ScriptSig = input.TxIn.ScriptSig.ToHex(),
                ScriptSigAsm = input.TxIn.ScriptSig.ToString(),
                WitScript = input.TxIn.WitScript?.ToString() ?? string.Empty,
                SequenceLock = input.TxIn.Sequence.ToString()
            }).ToList(),
            Outputs = transaction.Outputs.AsIndexedOutputs().Select(output => new QueryTransactionOutput
            {
                Index = (int)output.N,
                Address = output.TxOut.ScriptPubKey.GetDestinationAddress(network)?.ToString() ?? string.Empty,
                Balance = output.TxOut.Value.Satoshi,
                ScriptPubKey = output.TxOut.ScriptPubKey.ToHex(),
                ScriptPubKeyAsm = output.TxOut.ScriptPubKey.ToString(),
                OutputType = GetScriptTypeBlockcore(output.TxOut.ScriptPubKey)
            }).ToList()
        };
    }

    private static string GetScriptTypeNBitcoin(NBitcoin.Script scriptPubKey)
    {
        // Check specific types before general types
        if (scriptPubKey.IsScriptType(NBitcoin.ScriptType.Taproot))
            return "taproot";
        if (scriptPubKey.IsScriptType(NBitcoin.ScriptType.P2WPKH))
            return "witness_v0_keyhash";
        if (scriptPubKey.IsScriptType(NBitcoin.ScriptType.P2WSH))
            return "witness_v0_scripthash";
        if (scriptPubKey.IsScriptType(NBitcoin.ScriptType.Witness))
            return "witness";
        if (scriptPubKey.IsScriptType(NBitcoin.ScriptType.P2PKH))
            return "pubkeyhash";
        if (scriptPubKey.IsScriptType(NBitcoin.ScriptType.P2SH))
            return "scripthash";
        return "unknown";
    }

    private static string GetScriptTypeBlockcore(Blockcore.Consensus.ScriptInfo.Script scriptPubKey)
    {
        var scriptBytes = scriptPubKey.ToBytes();
        
        // Taproot: OP_1 followed by 32 bytes
        if (scriptBytes.Length == 34 && scriptBytes[0] == 0x51 && scriptBytes[1] == 32)
            return "taproot";
        
        // P2WPKH: OP_0 followed by 20 bytes
        if (scriptBytes.Length == 22 && scriptBytes[0] == 0x00 && scriptBytes[1] == 20)
            return "witness_v0_keyhash";
        
        // P2WSH: OP_0 followed by 32 bytes
        if (scriptBytes.Length == 34 && scriptBytes[0] == 0x00 && scriptBytes[1] == 32)
            return "witness_v0_scripthash";
        
        // P2PKH: OP_DUP OP_HASH160 <20 bytes> OP_EQUALVERIFY OP_CHECKSIG
        if (scriptBytes.Length == 25 && scriptBytes[0] == 0x76 && scriptBytes[1] == 0xa9 && scriptBytes[2] == 20)
            return "pubkeyhash";
        
        // P2SH: OP_HASH160 <20 bytes> OP_EQUAL
        if (scriptBytes.Length == 23 && scriptBytes[0] == 0xa9 && scriptBytes[1] == 20)
            return "scripthash";
        
        return "unknown";
    }
}
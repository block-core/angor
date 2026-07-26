using Angor.Shared.Models;
using NBitcoin;
using Transaction = NBitcoin.Transaction;

namespace Angor.Shared.Protocol;

/// <summary>
/// No-op transaction transformer for Bitcoin networks.
/// All methods return their inputs unchanged since no transformation is needed.
/// </summary>
public class BitcoinTransactionTransformer : ITransactionTransformer
{
    public TransactionInfo WrapP2WPKH(
        TransactionInfo bitcoinTxInfo,
        List<Blockcore.NBitcoin.Coin> coins,
        List<Blockcore.NBitcoin.Key> keys)
    {
        return bitcoinTxInfo;
    }

    public TransactionInfo WrapTaproot(
        TransactionInfo bitcoinTxInfo,
        Key nbitcoinKey,
        TxOut[] spentOutputs)
    {
        return bitcoinTxInfo;
    }

    public SignatureInfo WrapTaprootSignatures(
        SignatureInfo signatureInfo,
        Transaction recoveryTx,
        TxOut[] spentOutputs,
        Key nbitcoinKey,
        Func<int, Script> scriptPerStage)
    {
        return signatureInfo;
    }

    public TransactionInfo WrapP2WSH(
        TransactionInfo bitcoinTxInfo,
        Key nbitcoinKey,
        Script spendingScript,
        Transaction recoveryTransaction)
    {
        return bitcoinTxInfo;
    }

    public Transaction GetSighashTransaction(Transaction bitcoinTx)
    {
        return bitcoinTx;
    }

    public TxOut[] GetSighashSpentOutputs(TxOut[] spentOutputs)
    {
        return spentOutputs;
    }
}

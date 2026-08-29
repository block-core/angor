using Angor.Shared.Models;
using NBitcoin;
using Transaction = NBitcoin.Transaction;

namespace Angor.Shared.Protocol;

/// <summary>
/// Transforms transactions for the target network (Bitcoin or Liquid).
/// On Bitcoin, all methods are no-ops returning their inputs unchanged.
/// On Liquid, methods rebuild transactions as Elements format with explicit L-BTC asset tags.
/// </summary>
public interface ITransactionTransformer
{
    /// <summary>
    /// Wraps a fully-signed P2WPKH transaction for the target network.
    /// </summary>
    TransactionInfo WrapP2WPKH(
        TransactionInfo bitcoinTxInfo,
        List<Blockcore.NBitcoin.Coin> coins,
        List<Blockcore.NBitcoin.Key> keys);

    /// <summary>
    /// Wraps a fully-signed Taproot script-path transaction for the target network.
    /// </summary>
    TransactionInfo WrapTaproot(
        TransactionInfo bitcoinTxInfo,
        Key nbitcoinKey,
        TxOut[] spentOutputs);

    /// <summary>
    /// Wraps Taproot signature strings for the target network.
    /// Re-computes sighash on the network-appropriate version of the recovery transaction
    /// and re-signs each stage.
    /// </summary>
    SignatureInfo WrapTaprootSignatures(
        SignatureInfo signatureInfo,
        Transaction recoveryTx,
        TxOut[] spentOutputs,
        Key nbitcoinKey,
        Func<int, Script> scriptPerStage);

    /// <summary>
    /// Wraps a fully-signed P2WSH transaction for the target network.
    /// </summary>
    TransactionInfo WrapP2WSH(
        TransactionInfo bitcoinTxInfo,
        Key nbitcoinKey,
        Script spendingScript,
        Transaction recoveryTransaction);

    /// <summary>
    /// Returns the transaction to use for sighash computation.
    /// On Bitcoin, returns the input unchanged. On Liquid, returns an Elements transaction.
    /// </summary>
    Transaction GetSighashTransaction(Transaction bitcoinTx);

    /// <summary>
    /// Returns the spent outputs to use for Taproot sighash computation.
    /// On Bitcoin, returns the input unchanged. On Liquid, returns Elements-format outputs.
    /// </summary>
    TxOut[] GetSighashSpentOutputs(TxOut[] spentOutputs);
}

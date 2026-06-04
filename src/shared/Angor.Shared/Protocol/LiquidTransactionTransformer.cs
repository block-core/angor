using NBitcoin;
using NBitcoin.Altcoins;
using NBitcoin.Altcoins.Elements;
using Angor.Shared.Models;

namespace Angor.Shared.Protocol;

/// <summary>
/// Transaction transformer for Liquid (Elements) networks.
/// Wraps fully-signed Bitcoin transactions and re-creates them as valid Liquid transactions
/// with explicit L-BTC asset tags and an explicit fee output, then re-signs.
/// </summary>
public class LiquidTransactionTransformer : ITransactionTransformer
{
    private static readonly uint256 LBtcAssetId = ElementsParams<Liquid>.PeggedAssetId;
    private static readonly Network LiquidNetwork = Liquid.Instance.Mainnet;

    private readonly Blockcore.Networks.Network blockcoreNetwork;

    public LiquidTransactionTransformer(Blockcore.Networks.Network blockcoreNetwork)
    {
        this.blockcoreNetwork = blockcoreNetwork;
    }

    public TransactionInfo WrapP2WPKH(
        TransactionInfo bitcoinTxInfo,
        List<Blockcore.NBitcoin.Coin> coins,
        List<Blockcore.NBitcoin.Key> keys)
    {
        var fee = bitcoinTxInfo.TransactionFee;
        var bitcoinTx = Transaction.Parse(bitcoinTxInfo.Transaction.ToHex(), Network.Main);
        var elementsTx = BuildElementsTransaction(bitcoinTx, Money.Satoshis(fee));

        var builder = LiquidNetwork.CreateTransactionBuilder();

        foreach (var coin in coins)
        {
            var elemCoin = new Coin(
                new OutPoint(new uint256(coin.Outpoint.Hash.ToString()), coin.Outpoint.N),
                CreateExplicitOutput(
                    Money.Satoshis(coin.Amount.Satoshi),
                    new Script(coin.ScriptPubKey.ToBytes())));
            builder.AddCoins(elemCoin);
        }

        foreach (var blockcoreKey in keys)
        {
            builder.AddKeys(BlockcoreKeyToNBitcoinKey(blockcoreKey));
        }

        builder.SignTransactionInPlace(elementsTx);

        return CreateTransactionInfo(elementsTx, fee, bitcoinTxInfo);
    }

    public TransactionInfo WrapTaproot(
        TransactionInfo bitcoinTxInfo,
        Key nbitcoinKey,
        TxOut[] spentOutputs)
    {
        var fee = bitcoinTxInfo.TransactionFee;
        var bitcoinTx = Transaction.Parse(bitcoinTxInfo.Transaction.ToHex(), Network.Main);
        var elementsTx = BuildElementsTransaction(bitcoinTx, Money.Satoshis(fee));
        var elemSpentOutputs = TransformSpentOutputs(spentOutputs);
        var trxData = elementsTx.PrecomputeTransactionData(elemSpentOutputs);

        const TaprootSigHash sigHash = TaprootSigHash.All;

        for (int i = 0; i < elementsTx.Inputs.Count; i++)
        {
            var bitcoinWit = bitcoinTx.Inputs[i].WitScript;
            var scriptToExecute = new Script(bitcoinWit[bitcoinWit.PushCount - 2]);
            var controlBlockBytes = bitcoinWit[bitcoinWit.PushCount - 1];

            var leafHash = scriptToExecute.ToTapScript(TapLeafVersion.C0).LeafHash;
            var hash = elementsTx.GetSignatureHashTaproot(trxData,
                new TaprootExecutionData(i, leafHash) { SigHash = sigHash });

            var sig = nbitcoinKey.SignTaprootKeySpend(hash, sigHash);

            var witOps = new List<byte[]>();
            witOps.Add(sig.ToBytes());
            for (int p = 1; p < bitcoinWit.PushCount - 2; p++)
            {
                witOps.Add(bitcoinWit[p]);
            }
            witOps.Add(scriptToExecute.ToBytes());
            witOps.Add(controlBlockBytes);

            elementsTx.Inputs[i].WitScript = new WitScript(
                witOps.Select(Op.GetPushOp).ToArray());
        }

        return CreateTransactionInfo(elementsTx, fee, bitcoinTxInfo);
    }

    public SignatureInfo WrapTaprootSignatures(
        SignatureInfo signatureInfo,
        Transaction recoveryTx,
        TxOut[] spentOutputs,
        Key nbitcoinKey,
        Func<int, Script> scriptPerStage)
    {
        var elementsTx = BuildElementsTransaction(recoveryTx, Money.Zero);
        var elemSpentOutputs = TransformSpentOutputs(spentOutputs);

        const TaprootSigHash sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        foreach (var sigItem in signatureInfo.Signatures)
        {
            var tapScript = scriptPerStage(sigItem.StageIndex).ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(sigItem.StageIndex, tapScript.LeafHash) { SigHash = sigHash };
            var hash = elementsTx.GetSignatureHashTaproot(elemSpentOutputs, execData);

            var sig = nbitcoinKey.SignTaprootKeySpend(hash, sigHash);
            sigItem.Signature = sig.ToString();
        }

        return signatureInfo;
    }

    public TransactionInfo WrapP2WSH(
        TransactionInfo bitcoinTxInfo,
        Key nbitcoinKey,
        Script spendingScript,
        Transaction recoveryTransaction)
    {
        var fee = bitcoinTxInfo.TransactionFee;
        var bitcoinTx = Transaction.Parse(bitcoinTxInfo.Transaction.ToHex(), Network.Main);
        var elementsTx = BuildElementsTransaction(bitcoinTx, Money.Satoshis(fee));

        var builder = LiquidNetwork.CreateTransactionBuilder();

        foreach (var input in elementsTx.Inputs)
        {
            var spendingOutput = recoveryTransaction.Outputs.AsIndexedOutputs()
                .First(f => f.N == input.PrevOut.N);

            var elemOutput = CreateExplicitOutput(spendingOutput.TxOut.Value, spendingOutput.TxOut.ScriptPubKey);
            var coin = new Coin(input.PrevOut, elemOutput);
            var scriptCoin = coin.ToScriptCoin(spendingScript);
            builder.AddCoins(scriptCoin);
        }

        builder.AddKeys(nbitcoinKey);
        builder.SignTransactionInPlace(elementsTx);

        return CreateTransactionInfo(elementsTx, fee, bitcoinTxInfo);
    }

    public Transaction GetSighashTransaction(Transaction bitcoinTx)
    {
        return BuildElementsTransaction(bitcoinTx, Money.Zero);
    }

    public TxOut[] GetSighashSpentOutputs(TxOut[] spentOutputs)
    {
        return TransformSpentOutputs(spentOutputs);
    }

    private Transaction BuildElementsTransaction(Transaction bitcoinTx, Money fee)
    {
        var elementsTx = LiquidNetwork.CreateTransaction();

        elementsTx.Version = bitcoinTx.Version;
        elementsTx.LockTime = bitcoinTx.LockTime;

        foreach (var input in bitcoinTx.Inputs)
        {
            var elemInput = (ElementsTxIn)LiquidNetwork.Consensus.ConsensusFactory.CreateTxIn();
            elemInput.PrevOut = input.PrevOut;
            elemInput.Sequence = input.Sequence;
            if (input.ScriptSig != null && input.ScriptSig != Script.Empty)
            {
                elemInput.ScriptSig = input.ScriptSig;
            }

            elementsTx.Inputs.Add(elemInput);
        }

        foreach (var output in bitcoinTx.Outputs)
        {
            elementsTx.Outputs.Add(CreateExplicitOutput(output.Value, output.ScriptPubKey));
        }

        if (fee > Money.Zero)
        {
            elementsTx.Outputs.Add(CreateExplicitOutput(fee, Script.Empty));
        }

        return elementsTx;
    }

    private static ElementsTxOut<Liquid> CreateExplicitOutput(Money amount, Script scriptPubKey)
    {
        var elemOutput = new ElementsTxOut<Liquid>();
        elemOutput.Value = amount;
        elemOutput.ScriptPubKey = scriptPubKey;
        elemOutput.Asset = new ConfidentialAsset<Liquid>(LBtcAssetId);
        elemOutput.Nonce = new ConfidentialNonce();
        return elemOutput;
    }

    private static TxOut[] TransformSpentOutputs(TxOut[] spentOutputs)
    {
        return spentOutputs.Select(o => (TxOut)CreateExplicitOutput(o.Value, o.ScriptPubKey)).ToArray();
    }

    private Key BlockcoreKeyToNBitcoinKey(Blockcore.NBitcoin.Key blockcoreKey)
    {
        var wif = blockcoreKey.GetBitcoinSecret(blockcoreNetwork).ToString();
        return Key.Parse(wif, LiquidNetwork);
    }

    private static TransactionInfo CreateTransactionInfo(
        Transaction signedElementsTx,
        long fee,
        TransactionInfo originalBitcoinTxInfo)
    {
        return new TransactionInfo
        {
            Transaction = originalBitcoinTxInfo.Transaction,
            TransactionHex = signedElementsTx.ToHex(),
            TransactionId = signedElementsTx.GetHash().ToString(),
            TransactionFee = fee
        };
    }
}

using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using NBitcoin;
using Microsoft.Extensions.Logging;

namespace Angor.Shared;

public class PsbtOperations : IPsbtOperations
{
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<PsbtOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IWalletOperations _walletOperations;
    private readonly IIndexerService _indexerService;

    private const int AccountIndex = 0; // for now only account 0
    private const int Purpose = 84; // for now only legacy

    public PsbtOperations(IIndexerService indexerService, IHdOperations hdOperations, ILogger<PsbtOperations> logger, INetworkConfiguration networkConfiguration, IWalletOperations walletOperations)
    {
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _walletOperations = walletOperations;
        _indexerService = indexerService;
    }

    public PsbtData CreatePsbtForTransaction(Transaction transaction, AccountInfo accountInfo, long feeRate, string? changeAddress = null, List<UtxoDataWithPath>? utxoDataWithPaths = null)
    {
        if (string.IsNullOrEmpty(accountInfo.RootExtPubKey))
        {
            throw new ApplicationException("The Root ExtPubKey is missing");
        }

        AngorNetwork network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = network.BitcoinNetwork;

        changeAddress ??= accountInfo.GetNextChangeReceiveAddress();

        if (utxoDataWithPaths == null || utxoDataWithPaths.Count == 0)
        {
            utxoDataWithPaths = _walletOperations.FindOutputsForTransaction(transaction.Outputs.Sum(txOut => txOut.Value.Satoshi), accountInfo);
        }
        
        if (!utxoDataWithPaths.Any())
            throw new ApplicationException("No coins found to fund the transaction.");

        var unsignedTx = Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);

        foreach (var coin in utxoDataWithPaths)
        {
            OutPoint outputint = new OutPoint(new uint256(coin.UtxoData.outpoint.transactionId), coin.UtxoData.outpoint.outputIndex);
            if (unsignedTx.Inputs.Any(x => x.PrevOut == outputint))
                continue;
            var txin = new TxIn(outputint, null);
            txin.WitScript = new WitScript(Op.GetPushOp(new byte[72]), Op.GetPushOp(new byte[33])); // for total size calculation
            unsignedTx.Inputs.Add(txin);
        }

        var totalSize = unsignedTx.GetVirtualSize();
        var totalFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(totalSize);

        var totalSatsIn = utxoDataWithPaths.Sum(s => s.UtxoData.value);
        var totalSatsOut = unsignedTx.Outputs.Sum(o => o.Value);
        var totalToChange = totalSatsIn - totalSatsOut - totalFee;

        var spendAll = totalSatsIn == totalSatsOut;
        if (spendAll)
        {
            var lastOutput = unsignedTx.Outputs.Last();

            if (totalFee >= lastOutput.Value)
                throw new ApplicationException($"The fee {totalFee} is greater then the last output {lastOutput.Value}");

            lastOutput.Value -= totalFee;
        }
        else
        {
            if (totalToChange <= 294)
            {
                throw new ApplicationException($"The change amount {totalToChange} is below the dust threshold for SegWit transactions.");
            }

            unsignedTx.Outputs.Add(new TxOut(Money.Satoshis(totalToChange.Satoshi), BitcoinAddress.Create(changeAddress, nbitcoinNetwork).ScriptPubKey));
        }

        var psbt = PSBT.FromTransaction(unsignedTx, nbitcoinNetwork);

        ExtPubKey accountExtPubKey = ExtPubKey.Parse(accountInfo.RootExtPubKey, nbitcoinNetwork);

        for (int i = 0; i < unsignedTx.Inputs.Count; i++)
        {
            var input = unsignedTx.Inputs[i];
            var utxoInfo = utxoDataWithPaths.FirstOrDefault(u => u.UtxoData.outpoint.ToString() == input.PrevOut.ToString());

            if (utxoInfo == null)
                throw new InvalidOperationException($"Could not find UTXO information for input {input.PrevOut}");

            psbt.Inputs[i].WitnessUtxo = new TxOut(Money.Satoshis(utxoInfo.UtxoData.value), Script.FromHex(utxoInfo.UtxoData.scriptHex));

            var keyPath = new KeyPath(utxoInfo.HdPath);
            var rootedKeyPath = new RootedKeyPath(accountExtPubKey.PubKey.GetHDFingerPrint(), keyPath);

            var pubKey = _hdOperations.GeneratePublicKey(ExtPubKey.Parse(accountInfo.ExtPubKey, nbitcoinNetwork), (int)keyPath.Indexes[4], keyPath.Indexes[3] == 1);
            var path = _hdOperations.CreateHdPath(Purpose, network.CoinType, AccountIndex, keyPath.Indexes[3] == 1, (int)keyPath.Indexes[4]);

            if (path != utxoInfo.HdPath)
                throw new InvalidOperationException($"Path does not match {path} {utxoInfo.HdPath}");

            psbt.Inputs[i].HDKeyPaths.Add(pubKey, rootedKeyPath);
        }

        return new PsbtData { PsbtHex = psbt.ToHex() };
    }

    public PsbtData CreatePsbtForTransactionFee(Transaction transaction, Transaction sourceTransaction, AccountInfo accountInfo, long feeRate)
    {
        if (string.IsNullOrEmpty(accountInfo.RootExtPubKey))
        {
            throw new ApplicationException("The Root ExtPubKey is missing");
        }

        AngorNetwork network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = network.BitcoinNetwork;

        var changeAddress = accountInfo.GetNextChangeReceiveAddress();

        var unsignedTx = Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);
        var fromTransaction = Transaction.Parse(sourceTransaction.ToHex(), nbitcoinNetwork);

        var totalSize = unsignedTx.GetVirtualSize();
        var totalFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(totalSize);

        var utxoDataWithPaths = _walletOperations.FindOutputsForTransaction(totalFee.Satoshi, accountInfo);

        if (!utxoDataWithPaths.Any())
            throw new ApplicationException("No coins found to fund the transaction.");

        foreach (var coin in utxoDataWithPaths)
        {
            OutPoint outputint = new OutPoint(new uint256(coin.UtxoData.outpoint.transactionId), coin.UtxoData.outpoint.outputIndex);
            if (unsignedTx.Inputs.Any(x => x.PrevOut == outputint))
                continue;
            var txin = new TxIn(outputint, null);
            txin.WitScript = new WitScript(Op.GetPushOp(new byte[72]), Op.GetPushOp(new byte[33])); // for total size calculation
            unsignedTx.Inputs.Add(txin);
        }

        var totalSatsFee = utxoDataWithPaths.Sum(s => s.UtxoData.value);
        var totalToChange = totalSatsFee- totalFee;

        if (totalToChange <= 294)
        {
            throw new ApplicationException($"The change amount {totalToChange} is below the dust threshold for SegWit transactions.");
        }

        unsignedTx.Outputs.Add(new TxOut(Money.Satoshis(totalToChange.Satoshi), BitcoinAddress.Create(changeAddress, nbitcoinNetwork).ScriptPubKey));

        var psbt = PSBT.FromTransaction(unsignedTx, nbitcoinNetwork);

        ExtPubKey accountExtPubKey = ExtPubKey.Parse(accountInfo.RootExtPubKey, nbitcoinNetwork);

        for (int i = 0; i < unsignedTx.Inputs.Count; i++)
        {
            var input = unsignedTx.Inputs[i];
            var psbtInput = psbt.Inputs.Single(p => p.PrevOut == input.PrevOut);

            var utxoInfo = utxoDataWithPaths.FirstOrDefault(u => u.UtxoData.outpoint.ToString() == input.PrevOut.ToString());

            if (utxoInfo == null)
            {
                var utxo = fromTransaction.Outputs.AsIndexedOutputs().FirstOrDefault(output => output.N == input.PrevOut.N);
                if (fromTransaction.GetHash() == input.PrevOut.Hash && utxo != null)
                {
                    // we assume the input is already signed so just copy the UTXO information
                    psbtInput.FinalScriptWitness = input.WitScript;
                    psbtInput.WitnessUtxo = utxo.TxOut;
                    continue;
                }

                throw new InvalidOperationException($"Could not find UTXO information for input {input.PrevOut}");
            }

            psbtInput.WitnessUtxo = new TxOut(Money.Satoshis(utxoInfo.UtxoData.value), Script.FromHex(utxoInfo.UtxoData.scriptHex));

            var keyPath = new KeyPath(utxoInfo.HdPath);
            var rootedKeyPath = new RootedKeyPath(accountExtPubKey.PubKey.GetHDFingerPrint(), keyPath);

            var pubKey = _hdOperations.GeneratePublicKey(ExtPubKey.Parse(accountInfo.ExtPubKey, nbitcoinNetwork), (int)keyPath.Indexes[4], keyPath.Indexes[3] == 1);
            var path = _hdOperations.CreateHdPath(Purpose, network.CoinType, AccountIndex, keyPath.Indexes[3] == 1, (int)keyPath.Indexes[4]);

            if (path != utxoInfo.HdPath)
                throw new InvalidOperationException($"Path does not match {path} {utxoInfo.HdPath}");

            psbtInput.HDKeyPaths.Add(pubKey, rootedKeyPath);
        }

        return new PsbtData { PsbtHex = psbt.ToHex() };
    }

    public TransactionInfo SignPsbt(PsbtData psbtData, WalletWords walletWords)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = network.BitcoinNetwork;

        var psbt = PSBT.Parse(psbtData.PsbtHex, nbitcoinNetwork);

        ExtKey extendedKey = walletWords.GetOrDeriveExtKey(_hdOperations);

        psbt.SignAll(ScriptPubKeyType.Segwit, extendedKey);

        if (!psbt.TryFinalize(out IList<PSBTError>? errors))
        {
            throw new PSBTException(errors);
        }

        Transaction signedTransaction = psbt.ExtractTransaction();

        Money fee = psbt.GetFee();

        return new TransactionInfo { Transaction = signedTransaction, TransactionFee = fee.Satoshi };
    }
}

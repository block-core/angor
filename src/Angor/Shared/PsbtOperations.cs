using Angor.Shared.Models;
using Angor.Shared.Services.Indexer;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.Networks;
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

        Network network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);

        changeAddress ??= accountInfo.GetNextChangeReceiveAddress();

        if (utxoDataWithPaths == null || utxoDataWithPaths.Count == 0)
        {
            utxoDataWithPaths = _walletOperations.FindOutputsForTransaction(transaction.Outputs.Sum(txOut => txOut.Value.Satoshi), accountInfo);
        }
        
        if (!utxoDataWithPaths.Any())
            throw new ApplicationException("No coins found to fund the transaction.");

        var unsignedTx = NBitcoin.Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);

        foreach (var coin in utxoDataWithPaths)
        {
            NBitcoin.OutPoint outputint = new NBitcoin.OutPoint(new NBitcoin.uint256(coin.UtxoData.outpoint.transactionId), coin.UtxoData.outpoint.outputIndex);
            if (unsignedTx.Inputs.Any(x => x.PrevOut == outputint))
                continue;
            var txin = new NBitcoin.TxIn(outputint, null);
            txin.WitScript = new NBitcoin.WitScript(NBitcoin.Op.GetPushOp(new byte[72]), NBitcoin.Op.GetPushOp(new byte[33])); // for total size calculation
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

            unsignedTx.Outputs.Add(new NBitcoin.TxOut(NBitcoin.Money.Satoshis(totalToChange.Satoshi), NBitcoin.BitcoinAddress.Create(changeAddress, nbitcoinNetwork).ScriptPubKey));
        }

        var psbt = NBitcoin.PSBT.FromTransaction(unsignedTx, nbitcoinNetwork);

        NBitcoin.ExtPubKey accountExtPubKey = NBitcoin.ExtPubKey.Parse(accountInfo.RootExtPubKey, nbitcoinNetwork);

        for (int i = 0; i < unsignedTx.Inputs.Count; i++)
        {
            var input = unsignedTx.Inputs[i];
            var utxoInfo = utxoDataWithPaths.FirstOrDefault(u => u.UtxoData.outpoint.ToString() == input.PrevOut.ToString());

            if (utxoInfo == null)
                throw new InvalidOperationException($"Could not find UTXO information for input {input.PrevOut}");

            psbt.Inputs[i].WitnessUtxo = new NBitcoin.TxOut(NBitcoin.Money.Satoshis(utxoInfo.UtxoData.value), NBitcoin.Script.FromHex(utxoInfo.UtxoData.scriptHex));

            var keyPath = new NBitcoin.KeyPath(utxoInfo.HdPath);
            var rootedKeyPath = new NBitcoin.RootedKeyPath(accountExtPubKey, keyPath);

            var pubKey = _hdOperations.GeneratePublicKey(ExtPubKey.Parse(accountInfo.ExtPubKey, network), (int)keyPath.Indexes[4], keyPath.Indexes[3] == 1);
            var path = _hdOperations.CreateHdPath(Purpose, network.Consensus.CoinType, AccountIndex, keyPath.Indexes[3] == 1, (int)keyPath.Indexes[4]);

            if (path != utxoInfo.HdPath)
                throw new InvalidOperationException($"Path does not match {path} {utxoInfo.HdPath}");

            psbt.Inputs[i].HDKeyPaths.Add(new NBitcoin.PubKey(pubKey.ToBytes()), rootedKeyPath);
        }

        return new PsbtData { PsbtHex = psbt.ToHex() };
    }

    public PsbtData CreatePsbtForTransactionFee(Transaction transaction, Transaction sourceTransaction, AccountInfo accountInfo, long feeRate)
    {
        if (string.IsNullOrEmpty(accountInfo.RootExtPubKey))
        {
            throw new ApplicationException("The Root ExtPubKey is missing");
        }

        Network network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);

        var changeAddress = accountInfo.GetNextChangeReceiveAddress();

        var unsignedTx = NBitcoin.Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);
        var fromTransaction = NBitcoin.Transaction.Parse(sourceTransaction.ToHex(), nbitcoinNetwork);

        var totalSize = unsignedTx.GetVirtualSize();
        var totalFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(totalSize);

        var utxoDataWithPaths = _walletOperations.FindOutputsForTransaction(totalFee.Satoshi, accountInfo);

        if (!utxoDataWithPaths.Any())
            throw new ApplicationException("No coins found to fund the transaction.");

        foreach (var coin in utxoDataWithPaths)
        {
            NBitcoin.OutPoint outputint = new NBitcoin.OutPoint(new NBitcoin.uint256(coin.UtxoData.outpoint.transactionId), coin.UtxoData.outpoint.outputIndex);
            if (unsignedTx.Inputs.Any(x => x.PrevOut == outputint))
                continue;
            var txin = new NBitcoin.TxIn(outputint, null);
            txin.WitScript = new NBitcoin.WitScript(NBitcoin.Op.GetPushOp(new byte[72]), NBitcoin.Op.GetPushOp(new byte[33])); // for total size calculation
            unsignedTx.Inputs.Add(txin);
        }

        var totalSatsFee = utxoDataWithPaths.Sum(s => s.UtxoData.value);
        var totalToChange = totalSatsFee- totalFee;

        if (totalToChange <= 294)
        {
            throw new ApplicationException($"The change amount {totalToChange} is below the dust threshold for SegWit transactions.");
        }

        unsignedTx.Outputs.Add(new NBitcoin.TxOut(NBitcoin.Money.Satoshis(totalToChange), NBitcoin.BitcoinAddress.Create(changeAddress, nbitcoinNetwork).ScriptPubKey));

        var psbt = NBitcoin.PSBT.FromTransaction(unsignedTx, nbitcoinNetwork);

        NBitcoin.ExtPubKey accountExtPubKey = NBitcoin.ExtPubKey.Parse(accountInfo.RootExtPubKey, nbitcoinNetwork);

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

            psbtInput.WitnessUtxo = new NBitcoin.TxOut(NBitcoin.Money.Satoshis(utxoInfo.UtxoData.value), NBitcoin.Script.FromHex(utxoInfo.UtxoData.scriptHex));

            var keyPath = new NBitcoin.KeyPath(utxoInfo.HdPath);
            var rootedKeyPath = new NBitcoin.RootedKeyPath(accountExtPubKey, keyPath);

            var pubKey = _hdOperations.GeneratePublicKey(ExtPubKey.Parse(accountInfo.ExtPubKey, network), (int)keyPath.Indexes[4], keyPath.Indexes[3] == 1);
            var path = _hdOperations.CreateHdPath(Purpose, network.Consensus.CoinType, AccountIndex, keyPath.Indexes[3] == 1, (int)keyPath.Indexes[4]);

            if (path != utxoInfo.HdPath)
                throw new InvalidOperationException($"Path does not match {path} {utxoInfo.HdPath}");

            psbtInput.HDKeyPaths.Add(new NBitcoin.PubKey(pubKey.ToBytes()), rootedKeyPath);
        }

        return new PsbtData { PsbtHex = psbt.ToHex() };
    }

    public TransactionInfo SignPsbt(PsbtData psbtData, WalletWords walletWords)
    {
        Network network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);

        var psbt = NBitcoin.PSBT.Parse(psbtData.PsbtHex, nbitcoinNetwork);

        ExtKey extendedKey = _hdOperations.GetExtendedKey(walletWords.Words, walletWords.Passphrase);

        var nbitcoinExtendedKey = NBitcoin.ExtKey.CreateFromBytes(extendedKey.ToBytes(network.Consensus.ConsensusFactory));

        psbt.SignAll(NBitcoin.ScriptPubKeyType.Segwit, nbitcoinExtendedKey);

        if (!psbt.TryFinalize(out IList<NBitcoin.PSBTError>? errors))
        {
            throw new NBitcoin.PSBTException(errors);
        }

        NBitcoin.Transaction signedTransaction = psbt.ExtractTransaction();

        NBitcoin.Money fee = psbt.GetFee();

        return new TransactionInfo { Transaction = network.CreateTransaction(signedTransaction.ToHex()), TransactionFee = fee.Satoshi };
    }
}
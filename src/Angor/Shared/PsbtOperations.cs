using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;

namespace Angor.Shared;

public class PsbtOperations : IPsbtOperations
{
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<WalletOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IWalletOperations _walletOperations;
    private readonly IIndexerService _indexerService;

    private const int AccountIndex = 0; // for now only account 0
    private const int Purpose = 84; // for now only legacy

    public PsbtOperations(IIndexerService indexerService, IHdOperations hdOperations, ILogger<WalletOperations> logger, INetworkConfiguration networkConfiguration, IWalletOperations walletOperations)
    {
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _walletOperations = walletOperations;
        _indexerService = indexerService;
    }

    public PsbtData CreatePsbtForTransaction(Transaction transaction, AccountInfo accountInfo, long feeRate, string? changeAddress = null)
    {
        if (string.IsNullOrEmpty(accountInfo.RootExtPubKey))
        {
            throw new ApplicationException("The Root ExtPubKey is missing");
        }

        Network network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);

        changeAddress = changeAddress ?? accountInfo.GetNextChangeReceiveAddress();

        var utxoDataWithPaths = _walletOperations.FindOutputsForTransaction((long)transaction.Outputs.Sum(_ => _.Value), accountInfo);
        var coins = utxoDataWithPaths.Select(u => new Coin(uint256.Parse(u.UtxoData.outpoint.transactionId), (uint)u.UtxoData.outpoint.outputIndex, Money.Satoshis(u.UtxoData.value), Script.FromHex(u.UtxoData.scriptHex))).ToList();

        if (!coins.Any())
            throw new ApplicationException("No coins found to fund the transaction.");

        var builder = new TransactionBuilder(network)
            .AddCoins(coins)
            .SetChange(BitcoinAddress.Create(changeAddress, network))
            .ContinueToBuild(transaction) // Add the predefined outputs
            .SendEstimatedFees(new FeeRate(Money.Satoshis(feeRate)))
            .CoverTheRest(); // Ensure enough input value covers outputs + fee, adjusting change if needed

        var unsignedTx = builder.BuildTransaction(false);

        var psbt = NBitcoin.PSBT.FromTransaction(NBitcoin.Transaction.Parse(unsignedTx.ToHex(), nbitcoinNetwork), nbitcoinNetwork);

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
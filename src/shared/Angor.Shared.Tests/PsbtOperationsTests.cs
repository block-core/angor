using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Angor.Shared.Services;
using Angor.Test.Protocol;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Test;

public class PsbtOperationsTests : AngorTestData
{
    private WalletOperations _walletOperations;
    private PsbtOperations _sut;

    private readonly Mock<IIndexerService> _indexerService;
    private readonly InvestorTransactionActions _investorTransactionActions;
    private readonly FounderTransactionActions _founderTransactionActions;
    private readonly IHdOperations _hdOperations;

    public PsbtOperationsTests()
    {
        _indexerService = new Mock<IIndexerService>();

        _walletOperations = new WalletOperations(_indexerService.Object, new HdOperations(), new NullLogger<WalletOperations>(), _networkConfiguration.Object);
        _sut = new PsbtOperations(_indexerService.Object, new HdOperations(), new NullLogger<PsbtOperations>(), _networkConfiguration.Object, _walletOperations);

        _investorTransactionActions = new InvestorTransactionActions(new NullLogger<InvestorTransactionActions>(),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
            new ProjectScriptsBuilder(_derivationOperations),
            new SpendingTransactionBuilder(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
            new InvestmentTransactionBuilder(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder()),
            new TaprootScriptBuilder(),
            _networkConfiguration.Object);

        _founderTransactionActions = new FounderTransactionActions(new NullLogger<FounderTransactionActions>(), _networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder());
    }


    private void AddCoins(AccountInfo accountInfo, int utxos, long amount)
    {
        var network = _networkConfiguration.Object.GetNetwork();

        int callCount = 0;
        _indexerService.Setup(_ => _.GetAdressBalancesAsync(It.IsAny<List<AddressInfo>>(), It.IsAny<bool>())).Returns((List<AddressInfo> info, bool conf) =>
        {
            if (callCount == 1)
                return Task.FromResult(Enumerable.Empty<AddressBalance>().ToArray());

            var res = info.Select(s => new AddressBalance { address = s.Address, balance = Money.Satoshis(amount).Satoshi }).ToArray();

            callCount++;
            return Task.FromResult(res);
        });

        int outputIndex = 0;
        _indexerService.Setup(_ => _.FetchUtxoAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns<string, int, int>((address, limit, offset) =>
        {
            var res = new List<UtxoData>
            {
                new ()
                {
                    address =address,
                    value = Money.Satoshis(amount).Satoshi,
                    outpoint = new Outpoint( uint256.Zero.ToString(),outputIndex++ ),
                    scriptHex = BitcoinAddress.Create(address, network.BitcoinNetwork).ScriptPubKey.ToHex()
                }
            };

            return Task.FromResult(res);
        });

        _walletOperations.UpdateDataForExistingAddressesAsync(accountInfo).Wait();

        _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo).Wait();
    }

    [Fact]
    public void AddFeeAndSignTransaction_test()
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        AccountInfo accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);

        AddCoins(accountInfo, 6, 500000000);

        var network = _networkConfiguration.Object.GetNetwork();

        var changeAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network.BitcoinNetwork).ToString();

        // Generate test data dynamically (previously used hardcoded Blockcore-format hex)
        var projectInfo = new ProjectInfo();
        projectInfo.TargetAmount = Money.Coins(3).Satoshi;
        projectInfo.StartDate = DateTime.UtcNow;
        projectInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInfo.PenaltyDays = 10;
        projectInfo.Stages = new List<Stage>
        {
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new Stage { AmountToRelease = 50, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInfo.FounderKey);
        projectInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(angorRootKey, projectInfo.FounderKey);

        var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, projectInfo.FounderKey);
        var investorKey = _derivationOperations.DeriveInvestorKey(words, projectInfo.FounderKey);
        var investorPrivateKey = _derivationOperations.DeriveInvestorPrivateKey(words, projectInfo.FounderKey);

        var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKey, Money.Coins(10).Satoshi);

        var psbtInvest = _sut.CreatePsbtForTransaction(investmentTransaction, accountInfo, 3000);
        var signedInvestmentTransaction = _sut.SignPsbt(psbtInvest, words);

        var strippedInvestmentTransaction = network.CreateTransaction(signedInvestmentTransaction.Transaction.ToHex());
        strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);

        var unsignedRecoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInfo, strippedInvestmentTransaction);
        var recoverySigs = _founderTransactionActions.SignInvestorRecoveryTransactions(projectInfo, strippedInvestmentTransaction.ToHex(), unsignedRecoveryTransaction, founderRecoveryPrivateKey);
        var recoveryTransaction = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs, investorPrivateKey);

        // remove the first stage to simulate that is was spent
        recoveryTransaction.Outputs.RemoveAt(0);
        recoveryTransaction.Inputs.RemoveAt(0);

        var psbt = _sut.CreatePsbtForTransactionFee(recoveryTransaction, signedInvestmentTransaction.Transaction, accountInfo, 3000);
        var recoveryTransactions = _sut.SignPsbt(psbt, words);

        // add the inputs of the investment trx
        List<Coin> coins = new();
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2))
        {
            coins.Add(new Coin(new OutPoint(signedInvestmentTransaction.Transaction.GetHash(), output.N), output.TxOut));
        }

        //add all utxos as coins (easier)
        foreach (var utxo in accountInfo.AddressesInfo.Concat(accountInfo.ChangeAddressesInfo).SelectMany(s => s.UtxoData))
        {
            coins.Add(new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                new Money(utxo.value), Script.FromHex(utxo.scriptHex))); //Adding fee inputs
        }

        TransactionValidation.ThanTheTransactionHasNoErrors(recoveryTransactions.Transaction, coins);
    }

    [Fact]
    public void AddInputsAndSignTransaction()
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        AccountInfo accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);

        AddCoins(accountInfo, 6, 500000000);

        var network = _networkConfiguration.Object.GetNetwork();

        var projectInfo = new ProjectInfo();
        projectInfo.TargetAmount = Money.Coins(3).Satoshi;
        projectInfo.StartDate = DateTime.UtcNow;
        projectInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInfo.PenaltyDays = 10;
        projectInfo.Stages = new List<Stage>
        {
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new Stage { AmountToRelease = 50, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInfo.FounderKey);
        projectInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(angorRootKey, projectInfo.FounderKey);

        var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, projectInfo.FounderKey);

        var investmentAmount = 10;
        var investorKey = _derivationOperations.DeriveInvestorKey(words, projectInfo.FounderKey);
        var investorPrivateKey = _derivationOperations.DeriveInvestorPrivateKey(words, projectInfo.FounderKey);

        var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKey, Money.Coins(investmentAmount).Satoshi);

        Assert.Equal(5, investmentTransaction.Outputs.Count); // 1 for angor fee, 1 for op return, 3 for stages
        Assert.Equal(247500000, investmentTransaction.Outputs[2].Value);
        Assert.Equal(247500000, investmentTransaction.Outputs[3].Value);
        Assert.Equal(495000000, investmentTransaction.Outputs[4].Value);

        var psbt = _sut.CreatePsbtForTransaction(investmentTransaction, accountInfo, 3000);
        var signedInvestmentTransaction = _sut.SignPsbt(psbt, words);

        var strippedInvestmentTransaction = network.CreateTransaction(signedInvestmentTransaction.Transaction.ToHex());
        strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);
        Assert.Equal(signedInvestmentTransaction.Transaction.GetHash(), strippedInvestmentTransaction.GetHash());

        var unsignedRecoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInfo, strippedInvestmentTransaction);
        var recoverySigs = _founderTransactionActions.SignInvestorRecoveryTransactions(projectInfo, strippedInvestmentTransaction.ToHex(), unsignedRecoveryTransaction, founderRecoveryPrivateKey);

        var sigCheckResult = _investorTransactionActions.CheckInvestorRecoverySignatures(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs);
        Assert.True(sigCheckResult, "failed to validate the founders signatures");

        var recoveryTransaction = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs, investorPrivateKey);

        // remove the first stage to simulate that is was spent
        recoveryTransaction.Outputs.RemoveAt(0);
        recoveryTransaction.Inputs.RemoveAt(0);

        var psbt1 = _sut.CreatePsbtForTransactionFee(recoveryTransaction, signedInvestmentTransaction.Transaction, accountInfo, 3000);
        var signedRecoveryTransaction = _sut.SignPsbt(psbt1, words);

        // add the inputs of the investment trx
        List<Coin> coins = new();
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2))
        {
            coins.Add(new Coin(new OutPoint(signedInvestmentTransaction.Transaction.GetHash(), output.N), output.TxOut));
        }

        //add all utxos as coins (easier)
        foreach (var utxo in accountInfo.AddressesInfo.Concat(accountInfo.ChangeAddressesInfo).SelectMany(s => s.UtxoData))
        {
            coins.Add(new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                new Money(utxo.value), Script.FromHex(utxo.scriptHex))); //Adding fee inputs
        }

        TransactionValidation.ThanTheTransactionHasNoErrors(signedRecoveryTransaction.Transaction, coins);
    }
}

using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Test.ProtocolNew;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Angor.Shared.Services;
using Money = Blockcore.NBitcoin.Money;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Test;

public class WalletOperationsTest : AngorTestData
{
    private WalletOperations _sut;

    private readonly Mock<IIndexerService> _indexerService;
    private readonly InvestorTransactionActions _investorTransactionActions;
    private readonly FounderTransactionActions _founderTransactionActions;

    public WalletOperationsTest()
    {
        _indexerService = new Mock<IIndexerService>();

        _sut = new WalletOperations(_indexerService.Object, new HdOperations(), new NullLogger<WalletOperations>(), _networkConfiguration.Object);

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
        _indexerService.Setup(_ => _.FetchUtxoAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns<string,int, int>((address, limit, offset) =>
        {
            var res = new List<UtxoData>
            {
                new ()
                {
                    address =address, 
                    value = Money.Satoshis(amount).Satoshi, 
                    outpoint = new Outpoint( uint256.Zero.ToString(),outputIndex++ ), 
                    scriptHex = new Blockcore.NBitcoin.BitcoinWitPubKeyAddress(address,network).ScriptPubKey.ToHex()
                }
            };

            return Task.FromResult(res);
        });

        _sut.UpdateDataForExistingAddressesAsync(accountInfo).Wait();

        _sut.UpdateAccountInfoWithNewAddressesAsync(accountInfo).Wait();
    }

    [Fact]
    public void AddFeeAndSignTransaction_test()
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        AccountInfo accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        AddCoins(accountInfo, 2, 500);

        var network = _networkConfiguration.Object.GetNetwork();

        var changeAddress = new Blockcore.NBitcoin.Key().PubKey.GetSegwitAddress(network).ToString();

        // to generate the hex for recoveryTransactionHex and investmentTransactionHex run the test method
        // InvestmentIntegrationsTests.SpendInvestorRecoveryTest and take the hex of investmentTransaction and signedRecoveryTransaction
        var recoveryTransactionHex = "01000000000103f65356f239b78188c6910500a59c7fd5acb8883b95bf5b27613d66b5d0ef3cb50200000000fffffffff65356f239b78188c6910500a59c7fd5acb8883b95bf5b27613d66b5d0ef3cb50300000000fffffffff65356f239b78188c6910500a59c7fd5acb8883b95bf5b27613d66b5d0ef3cb50400000000ffffffff03c0c62d000000000022002076e16f4355a210157c87efa95f08797caab991e44c2e2cd365e9b3832b1e9936c0c62d000000000022002076e16f4355a210157c87efa95f08797caab991e44c2e2cd365e9b3832b1e9936c0c62d000000000022002076e16f4355a210157c87efa95f08797caab991e44c2e2cd365e9b3832b1e99360441ab1f2ba7fc385fbc7fd3dceefa066e41b70fbd8366d8eb72e14100159bd4a67ef7422575d4f1ee49f9da87ea6adb9786a86beb5219abc67fbfd08d0afd4e867f834181f208d0c136be992efbfbf32aa102cc8b63e2326f52c500bda0587efa2cac710bc706ba4500cf8d7bf575676ab214cd049e01a4d8e3559b81118014c410df3983442084dbcf19c847ad564f17b01c22059de22c5c886d008652bcf0de433b90082a1cad203f52e32c8724abc4d1567012263e522ae41280a1aa2f66b90d5c36cae495cda3ac61c09f9aa7a903393a2d6aa6aa744355a25175b7ce04fcd081f04d10802bc90d10031a4f4dcd533b0cfe39f36c8f036d9665af27198c9c9c352c760de7beee5ec559cada3ecc29449db9add2e15737f3e63d6b5ee29b0cd7074402f8153d647fb991044112a0e54087556614fedc49db1ad230417e649deb83ca929564555dd207fc886e6e2936f1d539edb7f9cb115cfb2dedb5ac360f8d1675afa68e11326dbe28a7bc8341dfdd9bbdc614b2351c8fec8892eddd413f6d839e0303a73dcfe63cc83f13278bf36f4193b11ca95e277ff8841e9fc64a8d5a615538a8604865a350d46e2e5c7b83442084dbcf19c847ad564f17b01c22059de22c5c886d008652bcf0de433b90082a1cad203f52e32c8724abc4d1567012263e522ae41280a1aa2f66b90d5c36cae495cda3ac61c19f9aa7a903393a2d6aa6aa744355a25175b7ce04fcd081f04d10802bc90d10031a4f4dcd533b0cfe39f36c8f036d9665af27198c9c9c352c760de7beee5ec5591dcf93d2fad25fb17e81dd39a7069bcd2617b89266d6f0d71ce5369cc6e0ae100441027876985bbb765032c71747da7bc760218325edf3849c9cffdf4699b7b1e88d55cf83ec89b01154d8470ef3b62528a2b383e1b56aa78e3223e4d4eb61ad45ed834121be56156f144a58b865c3267ce04567fe4178321bf13f3abd7a4fd147a3ca4ca47e0fbb0e97f0ee39f745752cde2d1084f4a8dc45dd487c979b25719813798683442084dbcf19c847ad564f17b01c22059de22c5c886d008652bcf0de433b90082a1cad203f52e32c8724abc4d1567012263e522ae41280a1aa2f66b90d5c36cae495cda3ac61c09f9aa7a903393a2d6aa6aa744355a25175b7ce04fcd081f04d10802bc90d10031a4f4dcd533b0cfe39f36c8f036d9665af27198c9c9c352c760de7beee5ec55925521ec6ac111e3a389727524747fdcdbee064f6d509c054954cf159569b4b3000000000";
        var recoveryTransaction = network.CreateTransaction(recoveryTransactionHex);
        var investmentTransactionHex = "010000080005c0c62d000000000016001464159155125d0522f0d951d25bbc584f8ccc48990000000000000000236a2102ee6e8902e7a8a5f343bb768b8bed2245620c886af241e2b374a7906a7d4c20a1c0c62d000000000022512091ffed43dbf778e1a4ab3758fc82189e56a8d99a3224c2a22ee5d3397645ad50c0c62d00000000002251202636cae6c34f00da94453afb36468f0c803402c5c5262aa59bf42800c5c58af4c0c62d0000000000225120f7d4c02d3e8380f8e380d84cf4c383fb745f21a5a09817108075f710002787c300000000";
        var investmentTransaction = network.CreateTransaction(investmentTransactionHex);

        // remove the first stage to simulate that is was spent
        recoveryTransaction.Outputs.RemoveAt(0);
        recoveryTransaction.Inputs.RemoveAt(0);

        var recoveryTransactions = _sut.AddFeeAndSignTransaction(changeAddress, recoveryTransaction, words, accountInfo, new FeeEstimation { FeeRate = 3000 });

        // add the inputs of the investment trx
        List<Blockcore.NBitcoin.Coin> coins = new();
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2))
        {
            coins.Add(new Blockcore.NBitcoin.Coin(investmentTransaction, (uint)output.N));
        }

        //add all utxos as coins (easier)
        foreach (var utxo in accountInfo.AddressesInfo.Concat(accountInfo.ChangeAddressesInfo).SelectMany(s => s.UtxoData))
        {
            coins.Add(new Blockcore.NBitcoin.Coin(Blockcore.NBitcoin.uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                new Money(utxo.value), Blockcore.Consensus.ScriptInfo.Script.FromHex(utxo.scriptHex))); //Adding fee inputs
        }

        TransactionValidation.ThanTheTransactionHasNoErrors(recoveryTransactions.Transaction, coins);
    }

    [Fact]
    public void AddInputsAndSignTransaction()
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        AccountInfo accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        AddCoins(accountInfo, 6, 50000000);

        var network = _networkConfiguration.Object.GetNetwork();

        var projectInfo = new ProjectInfo();
        projectInfo.TargetAmount = 3;
        projectInfo.StartDate = DateTime.UtcNow;
        projectInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInfo.PenaltyDays= 10;
        projectInfo.Stages = new List<Stage>
        {
            new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, 1);
        projectInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(projectInfo.FounderKey, angorRootKey);

        var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

        var investmentAmount = 10;
        var investorKey = _derivationOperations.DeriveInvestorKey(words, projectInfo.FounderKey);
        var investorPrivateKey = _derivationOperations.DeriveInvestorPrivateKey(words, projectInfo.FounderKey);

        var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKey, Money.Coins(investmentAmount).Satoshi);
        var signedInvestmentTransaction = _sut.AddInputsAndSignTransaction(accountInfo.GetNextReceiveAddress(), investmentTransaction, words, accountInfo, new FeeEstimation { FeeRate = 3000 });
        var strippedInvestmentTransaction = network.CreateTransaction(signedInvestmentTransaction.Transaction.ToHex());
        strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = Blockcore.Consensus.TransactionInfo.WitScript.Empty);
        Assert.Equal(signedInvestmentTransaction.Transaction.GetHash(), strippedInvestmentTransaction.GetHash());

        var unsignedRecoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInfo, strippedInvestmentTransaction);
        var recoverySigs = _founderTransactionActions.SignInvestorRecoveryTransactions(projectInfo, strippedInvestmentTransaction.ToHex(), unsignedRecoveryTransaction, Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

        _investorTransactionActions.CheckInvestorRecoverySignatures(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs);

        var recoveryTransaction = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()));

        // remove the first stage to simulate that is was spent
        recoveryTransaction.Outputs.RemoveAt(0);
        recoveryTransaction.Inputs.RemoveAt(0);

        var signedRecoveryTransaction = _sut.AddFeeAndSignTransaction(accountInfo.GetNextReceiveAddress(), recoveryTransaction, words, accountInfo, new FeeEstimation { FeeRate = 3000 });

        // add the inputs of the investment trx
        List<Blockcore.NBitcoin.Coin> coins = new();
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2))
        {
            coins.Add(new Blockcore.NBitcoin.Coin(signedInvestmentTransaction.Transaction, (uint)output.N));
        }

        //add all utxos as coins (easier)
        foreach (var utxo in accountInfo.AddressesInfo.Concat(accountInfo.ChangeAddressesInfo).SelectMany(s => s.UtxoData))
        {
                coins.Add(new Blockcore.NBitcoin.Coin(Blockcore.NBitcoin.uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                    new Money(utxo.value), Blockcore.Consensus.ScriptInfo.Script.FromHex(utxo.scriptHex))); //Adding fee inputs
        }

        TransactionValidation.ThanTheTransactionHasNoErrors(signedRecoveryTransaction.Transaction, coins);
    }
}
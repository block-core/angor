using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Test.DataBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using Hashes = Blockcore.NBitcoin.Crypto.Hashes;
using Key = Blockcore.NBitcoin.Key;
using Mnemonic = Blockcore.NBitcoin.BIP39.Mnemonic;
using Money = Blockcore.NBitcoin.Money;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using WordCount = Blockcore.NBitcoin.BIP39.WordCount;
using Wordlist = Blockcore.NBitcoin.BIP39.Wordlist;

namespace Angor.Test.ProtocolNew;

public class FounderTransactionActionTest : AngorTestData
{
    private readonly FounderTransactionActions _sut;
    private readonly Mock<IWalletOperations> _walletOperations;

    private readonly FeeEstimation _expectedFeeEstimation = new()
        { Confirmations = 1, FeeRate = 10000 };

    public FounderTransactionActionTest()
    {
        _walletOperations = new Mock<IWalletOperations>();
        _walletOperations.Setup(_ => _.GetUnspentOutputsForTransaction(It.IsAny<WalletWords>(),
                It.IsAny<List<UtxoDataWithPath>>()))
            .Returns<WalletWords, List<UtxoDataWithPath>>((_, _) =>
            {
                var network = Networks.Bitcoin.Testnet();

                // create a fake inputTrx
                var fakeInputTrx = network.Consensus.ConsensusFactory.CreateTransaction();
                var fakeInputKey = new Key();
                var fakeTxout = fakeInputTrx.AddOutput(Money.Parse("20.2"), fakeInputKey.ScriptPubKey);

                var keys = new List<Key> { fakeInputKey };

                var coins = keys.Select(key => new Blockcore.NBitcoin.Coin(fakeInputTrx, fakeTxout)).ToList();

                return (coins, keys);
            });


        _sut = new FounderTransactionActions(new NullLogger<FounderTransactionActions>(), _networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder());

    }

    private Transaction GivenASeederTransaction(ProjectInfo projectInvestmentInfo)
    {
        InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, _derivationOperations);
        var network = Networks.Bitcoin.Testnet();
        var seederKey = new Key();
        var seederSecret = new Key();
        var seederChangeKey = new Key();

        InvestorContext seederContext = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

        seederContext.InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
        seederContext.ChangeAddress = seederChangeKey.PubKey.GetSegwitAddress(network).ToString();
        seederContext.InvestorSecretHash = Hashes.Hash256(seederSecret.ToBytes()).ToString();

        projectInvestmentInfo.ProjectSeeders.SecretHashes.Add(seederContext.InvestorSecretHash);

        return operations.CreateInvestmentTransaction(network, seederContext,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);
    }

    private Transaction GivenAnInvestorTransaction(ProjectInfo projectInvestmentInfo)
    {
        InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, _derivationOperations);

        var network = Networks.Bitcoin.Testnet();
        var seederKey = new Key();
        var seederChangeKey = new Key();

        InvestorContext context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

        context.InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
        context.ChangeAddress = seederChangeKey.PubKey.GetSegwitAddress(network).ToString();

        return operations.CreateInvestmentTransaction(network, context,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);
    }

    /// <summary>
    /// To recreate the vector parameters see GenerateVectorsFor_SignInvestorRecoveryTransactions_CreatesValidSignatures
    /// </summary>
    [Fact]
    public void SignInvestorRecoveryTransactions_CreatesValidSignatures()
    {
        var words = new WalletWords
            { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

        var investmentTrxHex =
            "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000236a21038a7eedf38d874799c0d7579d5f08d605ca039da7f6dc7c57e6abd82f0f380334e0930400000000002251207aade2c416ca565c1b66041eac56d707a26aa8bab7d217190ecf3aa57899c9a360e316000000000022512048dc0a52f43379c6515962a40eec91d183582c8e2c861d612e4e564617d08a67804f120000000000225120f57ad9ed9e0fb880cda0847cfea668a4c3ccb1b9a08bdbcc3f029e8ad0380c9d00000000";
        var recoveryTrxHex = "010000000396174a48b38bc96a0addafd1ccfdc478a53aae572c3cc5240b33231939eb67880200000000ffffffff96174a48b38bc96a0addafd1ccfdc478a53aae572c3cc5240b33231939eb67880300000000ffffffff96174a48b38bc96a0addafd1ccfdc478a53aae572c3cc5240b33231939eb67880400000000ffffffff03e09304000000000022002012f9e11fd7142b631007a4c8cde632a0f851fcc6b61fbd6f09ceebf30ea6d4eb60e316000000000022002012f9e11fd7142b631007a4c8cde632a0f851fcc6b61fbd6f09ceebf30ea6d4eb804f12000000000022002012f9e11fd7142b631007a4c8cde632a0f851fcc6b61fbd6f09ceebf30ea6d4eb00000000";
        var recoveryTransaction = Networks.Bitcoin.Testnet().CreateTransaction(recoveryTrxHex);
        var key = new NBitcoin.Key(founderRecoveryPrivateKey.ToBytes());
        var expectedHashes = new List<NBitcoin.uint256>()
        {
            new(Encoders.Hex.DecodeData("76e77d8738586b9bafd708421371314872165e9f113946a11bc1112e2fbc8f41")),
            new(Encoders.Hex.DecodeData("7f0fa60f9f991f291bdaea122635eaab8f3e1a7b29fcb4e5f2e270f0c11ba435")),
            new (Encoders.Hex.DecodeData("4e4f9533500faea374fd71e59ae3ef20cc9bb397f6a95ee6dcf47fc17e10708b"))
        };
        
        var result = _sut.SignInvestorRecoveryTransactions(projectInvestmentInfo, investmentTrxHex, recoveryTransaction, Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

        Assert.NotEmpty(result.Signatures);
        Assert.Equal(3,result.Signatures.Count);

        for (var index = 0; index < result.Signatures.Count; index++)
        {
            Assert.True(key.CreateTaprootKeyPair().PubKey.VerifySignature(
                expectedHashes[index], TaprootSignature.Parse(result.Signatures[index].Signature).SchnorrSignature));
        }
    }

    //[Fact]
    public void SpendFounderStage_withFixedData() //TODO fix the test 
    {
        int stageNumber = 1;
        
        var words = new WalletWords
            { Words = "saddle hawk note mind travel prison tragic three degree tongue duty tone" };
    
        var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
        var funderReceiveCoinsKey = new Key();

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words, new DateTime(638295190967868801));

        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders
        {
            Threshold = 2,
            SecretHashes = new List<string>
            {
                "c39301155c48e50db3a111c766c1836c36326e05ebfe13aea0e376c87be754ca",
                "56f248dcb956f36cd0363daa960bf5be4f21ee1c4cd5da22da2385335e3f08ba",
                "912fbd981001c737f95d9c03a0e0498d71e90bb91fffd3b8a7ed7ca6a530e8da"
            }
        };

    var transactionHexList = new List<string>
    {
        "010000080005c0c62d0000000000160014e356a6ff53f3875b9620492aaea98a89d964443c0000000000000000446a21033a61dfb8f31a67b61c8e15835d8ad4591bd728fbe7f3a112527d2a54241aba2220ca54e77bc876e3a0ae13feeb056e32366c83c166c711a1b30de5485c150193c380c3c901000000002251203e242cf22f15b7607455e6c04d95e18de5df6eb63238905a748eb42d2d08f88d80d1f00800000000225120c812227079c1d57395c9792bd176ea0e6ebafb1ce5e6a0772797386ce6bc40e5000e270700000000225120aada3b47492dc6b596f26c2c8fdaea92ae857477802cbfe6732e6c281d2a4a2d00000000",
        "010000080005c0c62d0000000000160014e356a6ff53f3875b9620492aaea98a89d964443c0000000000000000446a2103761c88ca2b5a814596906d41fdd2b504e92d24a06c6d39ba220a7dababd92b7220ba083f5e338523da22dad54c1cee214fbef50b96aa3d36d06cf356b9dc48f25680c3c901000000002251201e57ec396649f646de54c1f652be5cdac4e5a720615e915180ac6fd1cfdebce880d1f00800000000225120f0f2cd06b31d5d718707f938586df9c2ff0cf6872f551837650a6e7555db4b7f000e270700000000225120f407bb19550ae21f1b19d5c19562d491fac7caa1cb6af36e143ab0654fcacd4400000000",
        "010000080005c0c62d0000000000160014e356a6ff53f3875b9620492aaea98a89d964443c0000000000000000446a21027d58e79ed3d7a33d7b4ea19bac37519bbf5cba53783633ed82f334d82d44a06620dae830a5a67ceda7b8d3ff1fb90be9718d49e0a0039c5df937c7011098bd2f9180c3c90100000000225120bea6df9861c94095aedf36156671d743cb92af930ff5aea4386e985e5460208f80d1f008000000002251208042353ddf688b757b691a89c29c148e3a243c54ae834d31568eae8005f8d085000e2707000000002251202f45ea8c811102dabfb2d5620c7b58b6282d9362e1711bff5f985dc2e6f2e85100000000",
        "010000080005c0c62d0000000000160014e356a6ff53f3875b9620492aaea98a89d964443c0000000000000000236a210330dbb2f8c751a5cbef888b60b16c966a6093c7249c1b0f4299b2c58b53878f9280c3c90100000000225120b8d8807ef0f4a9105d115ad1d99cc4e64f15ae0037276c1a36e2b62ae0d782c980d1f008000000002251208f6b5cbeb2bbc5716ba321d5b370622c0fba13952267cdbf48f6c37c8f9208e9000e2707000000002251209055b51a3ac0ca18025269c26f78cd7150d51291daceb48aedee3c8e0d47e0ff00000000",
        "010000080005c0c62d0000000000160014e356a6ff53f3875b9620492aaea98a89d964443c0000000000000000236a2102e0ddb502660f739af1707ff32b2c52b32ad93e3b8087c8b7e1d9183e1f6b760880c3c9010000000022512084fb0619d5274b3306dd1199b738804af5391828bbcfb59308e43d4112d7470380d1f00800000000225120253c8e7093d4a6f6f8c50089ec60d525f0d4cbee231f458443c6e359381bda9b000e270700000000225120bcbdb8f7902320f889a1b1fd615293ddb3f55ee7f5f9dafb0ee8420bfd5c97d100000000"
    };

    var founderTrx = _sut.SpendFounderStage(projectInvestmentInfo, transactionHexList
        , 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderPrivateKey.ToBytes())
        , _expectedFeeEstimation);
        
        TransactionValidation.ThanTheTransactionHasNoErrors(founderTrx.Transaction,
            transactionHexList
                .Select(_ => Networks.Bitcoin.Testnet().CreateTransaction(_))
                .Select(_ => _.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1).ToCoin()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SpendFounderStage_TestUsingInvestmentOperations(int stageNumber)
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
        var funderReceiveCoinsKey = new Key();

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);
        
        var transactionList = new List<Transaction>();
        
        transactionList.Add(GivenASeederTransaction(projectInvestmentInfo));
        transactionList.Add(GivenASeederTransaction(projectInvestmentInfo));
        transactionList.Add(GivenASeederTransaction(projectInvestmentInfo));
        transactionList.Add(GivenAnInvestorTransaction(projectInvestmentInfo));
        transactionList.Add(GivenAnInvestorTransaction(projectInvestmentInfo));
        
        var founderTrxSpendStageOne = _sut.SpendFounderStage(projectInvestmentInfo,
            transactionList.Select(_ => _.ToHex()), stageNumber,
            funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderPrivateKey.ToBytes())
            , _expectedFeeEstimation);

        TransactionValidation.ThanTheTransactionHasNoErrors(founderTrxSpendStageOne.Transaction,
            transactionList.Select(_ => _.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1).ToCoin()));
    }

    public void GenerateVectorsFor_SignInvestorRecoveryTransactions_CreatesValidSignatures()
    {
        // this cod generates text vectors for SignInvestorRecoveryTransactions_CreatesValidSignatures

        //InvestorTransactionActions _investorTransactionActions;  // needed to recreate the vector parameters

        // needed to recreate the vector parameters
        //_investorTransactionActions = new InvestorTransactionActions(
        //    new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), 
        //    new ProjectScriptsBuilder(_derivationOperations),
        //    new SpendingTransactionBuilder(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
        //    new InvestmentTransactionBuilder(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder())), 
        //    new TaprootScriptBuilder(),
        //    _networkConfiguration.Object);

        // and in the method SignInvestorRecoveryTransactions use the method
        // Encoders.Hex.Encode() on the hash of each GetSignatureHashTaproot
        //var investorKey = _derivationOperations.DeriveInvestorKey(words, projectInvestmentInfo.FounderKey);
        //var build_investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo, investorKey,
        //    Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);
        //var build_recoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInvestmentInfo,
        //    build_investmentTransaction);
        //var build_founderSignatures = _sut.SignInvestorRecoveryTransactions(projectInvestmentInfo,
        //    build_investmentTransaction.ToHex(), build_recoveryTransaction, Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));
        //var build_investmentTransactionHex = build_investmentTransaction.ToHex();
        //var build_recoveryTransactionHex = build_recoveryTransaction.ToHex();

    }
}
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Moq;
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


        _sut = new FounderTransactionActions(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations),
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

    [Fact]
    public void SignInvestorRecoveryTransactions_()
    {
        var words = new WalletWords
            { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        var founderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
        var funderReceiveCoinsKey = new Key();

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders
        {
            Threshold = 2,
            SecretHashes = new List<string>
            {
                "e4759b9b7ea959f28b2594a14b3daf349178ba10405e1659fee35f763993e865",
                "7fafd2f15123babbbb4a187edeb7dc3c2c6d917f998d2e6ee3e3b02c929d17ac",
                "f5b567168806a449bd3b9ecc14f9fd63f478caad69169a9f1080508ea9f99075"
            }
        };

        var investmentTrxHex =
            "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000446a2103c298c205208c0c9e72528063f6fe5351d5c8d6db9c10a59f7c9447f858f31c3b2065e89339765fe3fe59165e4010ba789134af3d4ba194258bf259a97e9b9b75e480c3c9010000000022512017156ec0e463d67a17df8be2fd5fb4f4de965e8ddbbc1754e8c3748f9f178f7580d1f008000000002251207476a7cd846bd4cb4e9ce1b04bb9d542458ce1bc234a4d5c042e228e973f8e7b000e27070000000022512063ce95e900fe97d6521bcf038e3a27cbd8d809add5ca37602524df7428e765e700000000";

        projectInvestmentInfo.PenaltyDate = DateTime.Now.AddMinutes(1000);

        var investmentTrxBuilder = new InvestmentTransactionBuilder(_networkConfiguration.Object,
            new ProjectScriptsBuilder(_derivationOperations),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder())); 
        
        var transaction = investmentTrxBuilder.BuildUpfrontRecoverFundsTransaction(
            Networks.Bitcoin.Testnet().CreateTransaction(investmentTrxHex), 
            projectInvestmentInfo.PenaltyDate,
            Encoders.Hex.EncodeData(funderReceiveCoinsKey.PubKey.ToBytes()));

        var result = _sut.SignInvestorRecoveryTransactions(projectInvestmentInfo, investmentTrxHex, transaction, Encoders.Hex.EncodeData(founderPrivateKey.ToBytes()));

        Assert.NotEmpty(result);
        
        foreach (var signature in result)
        {
            //TODO get the hash for the verification
            // Assert.True(founderPrivateKey.PubKey.Verify(new uint256(), 
            //     new SchnorrSignature(TaprootSignature.Parse(signature).SchnorrSignature.ToBytes())));
        }
    }

    //[Fact]
    public void SpendFounderStage_withFixedData() //TODO fix the test 
    {
        int stageNumber = 1;
        
        var words = new WalletWords
            { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
    
        var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
        var funderReceiveCoinsKey = new Key();

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders
        {
            Threshold = 2,
            SecretHashes = new List<string>
            {
                "e4759b9b7ea959f28b2594a14b3daf349178ba10405e1659fee35f763993e865",
                "7fafd2f15123babbbb4a187edeb7dc3c2c6d917f998d2e6ee3e3b02c929d17ac",
                "f5b567168806a449bd3b9ecc14f9fd63f478caad69169a9f1080508ea9f99075"
            }
        };

    var transactionHexList = new List<string>
    {
        "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000446a2103c298c205208c0c9e72528063f6fe5351d5c8d6db9c10a59f7c9447f858f31c3b2065e89339765fe3fe59165e4010ba789134af3d4ba194258bf259a97e9b9b75e480c3c9010000000022512017156ec0e463d67a17df8be2fd5fb4f4de965e8ddbbc1754e8c3748f9f178f7580d1f008000000002251207476a7cd846bd4cb4e9ce1b04bb9d542458ce1bc234a4d5c042e228e973f8e7b000e27070000000022512063ce95e900fe97d6521bcf038e3a27cbd8d809add5ca37602524df7428e765e700000000",
        "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000446a210322da704c2813fc4c16d835a1910483e718624856dc9e77a6ea3b6cc2f3a38af320ac179d922cb0e3e36e2e8d997f916d2c3cdcb7de7e184abbbbba2351f1d2af7f80c3c90100000000225120e4f0734f5ae7f2113d00954cb4424c811ce4fba09a383ab649c35b88930c838380d1f00800000000225120a700c581da793a0997e86185f6acc81b408a6751fc51ad05f3a1c83dbb741897000e27070000000022512056da9954d605da2750a4eeb94fd2c27b6e58cd97ed446e0066248cef5bbe7edb00000000",
        "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000446a210335f4708588b886a3dc9a6662b03ba01978ef3cbad2b1b491b3480d1099b2aa50207590f9a98e5080109f9a1669adca78f463fdf914cc9e3bbd49a406881667b5f580c3c901000000002251205428cc8df1f7a3f6b4aa1d36cc6e107261880cd46bd7ee65c0268cedc0339a4c80d1f008000000002251207f1cbee8907c9f7b68ce81f69aefcc4ec533cc518ac30741e451b45be08db3f8000e2707000000002251206c43bf12d38ad3f49c0ccaacf825c6e8cc662b01e35e0336e329f17036ec631500000000",
        "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000236a21023bb34de4edd4e5874882a465fddb645bfdb083d0c0522538b69578b28f78431180c3c9010000000022512064d0ca5475744876a00ce33e2a7f82096b8e87ccbf464a8099357c745533ec9f80d1f008000000002251202fd3b34c25b33ba0b9a46b9a9e026d2831a6f7b3dd1609c13b1eeab853aa6391000e27070000000022512093b2cea402048c8c3c5e950a9adaec4624e861f5fe1b51af78014c15217bc4e400000000",
        "010000080005c0c62d0000000000160014e503a24793c82bf7f7eb18cfca6589df1360dcf40000000000000000236a2102e9e49625d2bae86bb7586a45bc0a1538feb0fa03b60d740076db76c8fbf000d480c3c9010000000022512024ae88230e2bff51e7921fb4bec7ad828ca2d3b45126eacd094816ec4c75812680d1f00800000000225120fdb6557adc1c53747910c45f7766090e37fc27ba394e1fb85738f84a58bb6aa0000e2707000000002251206a3d1c5e5648a21adbf2bda32ad2b4b471d4a70a2911dd1089449b5d9945323300000000"
    };

    var founderTrx = _sut.SpendFounderStage(projectInvestmentInfo, transactionHexList
        , 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderPrivateKey.ToBytes())
        , _expectedFeeEstimation);
        
        TransactionValidation.ThanTheTransactionHasNoErrors(founderTrx,
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

        TransactionValidation.ThanTheTransactionHasNoErrors(founderTrxSpendStageOne,
            transactionList.Select(_ => _.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1).ToCoin()));
    }
}
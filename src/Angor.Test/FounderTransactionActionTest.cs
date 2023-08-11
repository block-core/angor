using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin.Policy;
using Coin = NBitcoin.Coin;
using Key = Blockcore.NBitcoin.Key;

namespace Angor.Test;

public class FounderTransactionActionTest
{
    private FounderTransactionActions _sut;

    private Mock<INetworkConfiguration> _networkConfiguration;
    private DerivationOperations _derivationOperations;

    private Mock<IWalletOperations> _walletOperations;

    private string angorRootKey =
        "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";

    private FeeEstimation _expectedFeeEstimation = new FeeEstimation()
        { Confirmations = 1, FeeRate = 10000 };

    public FounderTransactionActionTest()
    {
        _networkConfiguration = new Mock<INetworkConfiguration>();
        _derivationOperations = new DerivationOperations(new HdOperations(), new NullLogger<DerivationOperations>(),
            _networkConfiguration.Object);

        _networkConfiguration.Setup(_ => _.GetNetwork())
            .Returns(Networks.Bitcoin.Testnet());

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


        _sut = new FounderTransactionActions(_networkConfiguration.Object);
    }

    // [Fact]
    // public void FounderSignInvestorRecoveryTransactionsSuccessfully()
    // {
    //     var words = new WalletWords
    //         { Words = "only velvet midnight beauty beef large mountain avocado south romance vault amount" };
    //
    //     var funderKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
    //     var funderReceiveCoinsKey = new Key();
    //
    //     var projectInvestmentInfo = new ProjectInfo
    //     {
    //         TargetAmount = 3,
    //         StartDate = DateTime.UtcNow,
    //         ExpiryDate = DateTime.UtcNow.AddDays(5),
    //         Stages = new List<Stage>
    //         {
    //             new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
    //             new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
    //             new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
    //         },
    //         FounderKey = _derivationOperations.DeriveFounderKey(words, 1)
    //     };
    //
    //     projectInvestmentInfo.ProjectIdentifier =
    //         _derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);
    //
    //     projectInvestmentInfo.ProjectSeeders = new ProjectSeeders
    //     {
    //         Threshold = 2,
    //         SecretHashes = new List<string>
    //         {
    //             "9f937baabec892fe456aff1907fb2503d6f10b126c31cc2cd07e29a7bfab5ed9",
    //             "b41eaf6867d84db46cd03a4cbac39245c6b8f01d656d7680419f4fb76a87b66c",
    //             "8b954955a414cfef28fae294adee85c0b6afc6472e52181f4f00804376defbba"
    //         }
    //     };
    //
    //
    //     var transactionHexList = new List<string>
    //     {
    //         "01000000010d3c7877e9472289c55d45844323d676ff71fda12192ea61885063c277ace4d6000000006a473044022040d9f53e8e9555d581bc319ba4808b3cd9a98c2184a8705dce1abf04210735d20220519808694cbb8de3be77d797ad6f307e97bb259ce72dc35c6119d65b34d7396201210242f8a4add0c9e29f5fb8d64b8a026d79fe7aeb311fca9c19592ec9c182375870ffffffff06c0c62d000000000016001495309ba4161be882445e36664f92fbb48bb9e8e40000000000000000446a2102bf89e6777f25ab7d8dc6536f32b191cfcc46f9bf828bda528d4d61eb05e28c2b20d95eabbfa7297ed02ccc316c120bf1d60325fb0719ff6a45fe92c8beaa7b939f80c3c9010000000022512037e828803bc173d996e5d639f8bd3232a9216b7d0d1e09427e769a3a8e003b23804a5d050000000022512066329cd444381bd77a80905f53dd7b0cda62f1ae76333c8f1e1aa28d08b138870087930300000000225120a6fb6384026e832ffedcae26bec7009e45a1974ee94b65ed3e2c19cb4ea3e48b9c547e6d0000000016001410d1c7d3780cd967570cbdddd7e6272ac8b6e86100000000",
    //         "01000000013b5a24b04a656f9a8d95332d47d3e7a23a82acb8dad518405f94a060907d746f000000006b483045022100abbc03215c347be901dfd80566b95c22dfa2af16e7cf84b3852af5af35cf2aa002204c5d895ea303b6eb53b3f466220bbf840b15fbc979fa125934a14ad725c08cac0121036ca989a768a097002521b3b6b4e4dcf6768bb38e83dadcbe873b8d0346f2400bffffffff06c0c62d000000000016001495309ba4161be882445e36664f92fbb48bb9e8e40000000000000000446a21020443033b848e26ee67c42423991742fa68813973b718ae37305fda11d709171c206cb6876ab74f9f4180766d651df0b8c64592c3ba4c3ad06cb44dd86768af1eb480c3c90100000000225120306cd0739c2b1603ce812a2ef4445e58cfc22b1efb1f8f812026e39e0eb9d468804a5d05000000002251209ad5faafad3dfe678f2e253b4474217ed81abe0d012eecfe3e871a15e7ce536f00879303000000002251202f04f496c1081f3d85125cbbb23e3ae1a2d4ee2f973788f752cc253be825446a9c547e6d0000000016001478fa0d9e3318334d8e8be9ceb7ff81e47f54ec6400000000",
    //         "010000000197138fd1b9221e6a3ca26eb29562d093cba49328f0ec230751fe6b06718a6f84000000006b483045022100e39070a85a8b25d5b4702b99f4bb5b80f052e8b641d01c51f39f271ccd96f74e0220380794235757d82d5428845bedbc636ce2fefbf80b004c52d23b7a3c2235b77a01210238d1624cb0acdefa842cb237546d6a7c47f74b1f638d4948e9418dbe2a25325affffffff06c0c62d000000000016001495309ba4161be882445e36664f92fbb48bb9e8e40000000000000000446a21036c4611cdc402f7f0e97c12a6104f61a4e8b08029a478106f7f7c20c32efcaee220bafbde764380004f1f18522e47c6afb6c085eead94e2fa28efcf14a45549958b80c3c9010000000022512059c66aedb2deb0e249a8b0be02f70cbebd9c0c4ea4419f0d555d124f434f8bcd804a5d050000000022512035128ee34e1f0d2439f239d34127dec1151dd8d3b7844342e5658db9ddc6aa4b008793030000000022512019bb59228f27da58c756a29a9d1d9faa8a11728390a6a307f564c24a181a78479c547e6d00000000160014f966e52eacf033e705ccc84486ae76fe62dd0b2600000000",
    //         "010000000166dd3cc67628cdcc74e33572c5c8973e53803b3cc0582df6c56cbeadedc8ae49000000006a473044022063674e32f6f168717642bf801accf27fd9473b3e393a85f5604b25540de1ce4e022053d1ddcf0d0d86cf62a1532ca4b6733182bab872e85b7e7e5c7272dbf4339c100121021fb7f14366ac8bf41cd8a9f00596854b9ab58bfec8c77405b056b305f932973fffffffff06c0c62d000000000016001495309ba4161be882445e36664f92fbb48bb9e8e40000000000000000236a2103b78a4b43149de19ea2c6f41b5f3065d34a5d805eb830b527376993d7d915401580c3c90100000000225120cc8650babdd007a658301985271f3a5417a738f8ac884b47f59324ac33343bb3804a5d0500000000225120930214929c7b9491d9ab3566d6f7c9751a9827fbd15286ea5e79bb81db947a8f00879303000000002251204da7b3c15cea0a369221b89d6a6123cb8716df4482ad4dab26fb15e93dc5c243e6557e6d000000001600142170e73a60dc3e6d8926edea132fa119ac2b7baf00000000",
    //         "01000000018ca960b97f94e2dc8ea2c4152b819c5f50c6e92bb1f3992028497ea62514822b000000006b483045022100ac1803d7b0b0b2ae889990e5c3dd7e28483c7393b82caa927f7220f12001b97f022040b544689f6e89f36918b817917cb59d02c32178e56642a78f2e1db882cb36fd012103d4b278d326b7b3b17af9a44493bcc8491e846a1dc268892f0fe58c6040fe2aa4ffffffff06c0c62d000000000016001495309ba4161be882445e36664f92fbb48bb9e8e40000000000000000236a21022eee5277ba0de48866c280584e7f9c2c362b2d208e3d466ce71e8d0b75c9b3bf80c3c90100000000225120f38acf39852892506a60d87e483f6a6b0af7d15ad0bea30dbd387b3c73cfa5b4804a5d05000000002251207141c785cb78a243fd53dd06ce31687d7533aa2f3d501eba16aff6170c12f7ae0087930300000000225120432947225334deb4dc1bf2730bb5c64988cce1e7e94c01e7af497e54b4a867cae6557e6d00000000160014b848630b518d2778d3eff5cbd4d49cc8b5c7871700000000"
    //     };
    //
    //     var founderTrx = _sut.SpendFounderStage(projectInvestmentInfo, transactionHexList
    //         , 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes())
    //         , _expectedFeeEstimation);
    //
    //     var nbitcoinNetwork = NetworkMapper.Map(Networks.Bitcoin.Testnet());
    //     var builder = nbitcoinNetwork.CreateTransactionBuilder();
    //
    //     foreach (var investmentTransactionHex in transactionHexList)
    //     {
    //         var investmentTransaction = NBitcoin.Transaction.Parse(investmentTransactionHex, nbitcoinNetwork);
    //
    //         builder.AddCoin(new Coin(investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(2)));
    //     }
    //
    //     var trx = NBitcoin.Transaction.Parse(founderTrx.ToHex(), nbitcoinNetwork);
    //
    //     Assert.True(builder.Verify(trx, out TransactionPolicyError[] errors),
    //         userMessage: errors.Select(_ => _.ToString()).Aggregate((x, y) => x + "," + y));
    // }


    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SpendFounderStage_Test(int stageNumber)
    {
        DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
            new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
        InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var funderKey = derivationOperations.DeriveFounderPrivateKey(words, 1);
        var funderReceiveCoinsKey = new Key();

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(derivationOperations, words);

        // create founder context
        var transactionHexList = new List<string>();

        // create seeder1 investment transaction

        transactionHexList.Add(GivenASeederTransactionHex(operations,  projectInvestmentInfo));
        transactionHexList.Add(GivenASeederTransactionHex(operations,  projectInvestmentInfo));
        transactionHexList.Add(GivenASeederTransactionHex(operations,  projectInvestmentInfo));
        transactionHexList.Add(GivenAnInvestorTransactionHex(operations,  projectInvestmentInfo));
        transactionHexList.Add(GivenAnInvestorTransactionHex(operations,  projectInvestmentInfo));

        // spend all investment transactions for stage 1
        var founderTrxSpendStageOne = _sut.SpendFounderStage(projectInvestmentInfo, transactionHexList, stageNumber,
            funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes())
            , _expectedFeeEstimation);


        var nbitcoinNetwork = NetworkMapper.Map(Networks.Bitcoin.Testnet());
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        foreach (var investmentTransactionHex in transactionHexList)
        {
            var investmentTransaction = NBitcoin.Transaction.Parse(investmentTransactionHex, nbitcoinNetwork);

            builder.AddCoin(new Coin(investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1)));
        }

        var trx = NBitcoin.Transaction.Parse(founderTrxSpendStageOne.ToHex(), nbitcoinNetwork);

        Assert.True(builder.Verify(trx, out TransactionPolicyError[] errors),
            userMessage: errors.Select(_ => _.ToString()).Aggregate("", (x, y) => x + "," + y));
    }

    private ProjectInfo GivenValidProjectInvestmentInfo(DerivationOperations derivationOperations, WalletWords words)
    {
        var projectInvestmentInfo = new ProjectInfo();
        projectInvestmentInfo.TargetAmount = 3;
        projectInvestmentInfo.StartDate = DateTime.UtcNow;
        projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInvestmentInfo.Stages = new List<Stage>
        {
            new Stage { AmountToRelease = (decimal)0.1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new Stage { AmountToRelease = (decimal)0.5, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new Stage { AmountToRelease = (decimal)0.4, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInvestmentInfo.FounderKey = derivationOperations.DeriveFounderKey(words, 1);
        projectInvestmentInfo.ProjectIdentifier =
            derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

        // Build seeders hashes

        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders { Threshold = 2 };
        return projectInvestmentInfo;
    }

    private string GivenASeederTransactionHex(InvestmentOperations operations, ProjectInfo projectInvestmentInfo)
    {
        var network = Networks.Bitcoin.Testnet();
        var seederKey = new Key();
        var seederSecret = new Key();
        var seederChangeKey = new Key();

        InvestorContext seederContext = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

        seederContext.InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
        seederContext.ChangeAddress = seederChangeKey.PubKey.GetSegwitAddress(network).ToString();
        seederContext.InvestorSecretHash = Hashes.Hash256(seederSecret.ToBytes()).ToString();
        
        projectInvestmentInfo.ProjectSeeders.SecretHashes.Add(seederContext.InvestorSecretHash);
        
        var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seederContext,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

        return operations.SignInvestmentTransaction(network, seederContext.ChangeAddress,
                seeder1InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
            .ToHex();
    }
    
    private string GivenAnInvestorTransactionHex(InvestmentOperations operations, ProjectInfo projectInvestmentInfo)
    {
        var network = Networks.Bitcoin.Testnet();
        var seederKey = new Key();
        var seederChangeKey = new Key();

        InvestorContext context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

        context.InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
        context.ChangeAddress = seederChangeKey.PubKey.GetSegwitAddress(network).ToString();

        var seeder1InvTrx = operations.CreateInvestmentTransaction(network, context,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

        return operations.SignInvestmentTransaction(network, context.ChangeAddress,
                seeder1InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
            .ToHex();
    }
}
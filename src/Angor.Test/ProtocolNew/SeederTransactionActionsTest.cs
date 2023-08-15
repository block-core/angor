using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Moq;

namespace Angor.Test.ProtocolNew;

public class SeederTransactionActionsTest : AngorTestData
{
    private SeederTransactionActions _sut;

    private Mock<IProjectScriptsBuilder> _projectScriptsBuilder;
    
    public SeederTransactionActionsTest()
    {
        _projectScriptsBuilder = new Mock<IProjectScriptsBuilder>();
        
        _sut = new SeederTransactionActions(_networkConfiguration.Object,
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
            _projectScriptsBuilder.Object);
    }

    private ProjectInfo GivenValidProjectInvestmentInfo( WalletWords words)
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
        projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInvestmentInfo.ProjectIdentifier =
            _derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);
        
        projectInvestmentInfo.ProjectSeeders = new ProjectSeeders { Threshold = 2 };
        return projectInvestmentInfo;
    }
    
    [Fact]
    public void SeederInvestmentTransactionCreation_addsAngorKeyScript()
        {
            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

            // Create the seeder 1 params
            var seederKey = new Key();
            var seedersecret = new Key();

            var investorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
            var investorSecret = Hashes.Hash256(seedersecret.ToBytes()).ToString();
            // create the investment transaction

            var expectedScript = new Key().ScriptPubKey;

            _projectScriptsBuilder.Setup(_ => _.GetAngorFeeOutputScript(projectInvestmentInfo.ProjectIdentifier))
                .Returns(expectedScript);
            
            var seederInvestmentTransaction = _sut.CreateInvestmentTransaction(projectInvestmentInfo, investorKey, investorSecret,
                Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            var expectedoutput = seederInvestmentTransaction.Outputs.First();
            
            Assert.True(expectedoutput.ScriptPubKey.Equals(expectedScript));
            Assert.Equal(projectInvestmentInfo.TargetAmount / 100,expectedoutput.Value.ToDecimal(MoneyUnit.BTC));
        }
    
    [Fact]
    public void SeederInvestmentTransactionCreation_addsOpReturnWithProjectData()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        // Create the seeder 1 params
        var seederKey = new Key();
        var seedersecret = new Key();

        var investorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
        var investorSecret = Hashes.Hash256(seedersecret.ToBytes()).ToString();
        // create the investment transaction

        var expectedScript = new Key().ScriptPubKey;

        _projectScriptsBuilder.Setup(_ => _.BuildSeederInfoScript(investorKey, investorSecret))
            .Returns(expectedScript);
            
        var seederInvestmentTransaction = _sut.CreateInvestmentTransaction(projectInvestmentInfo, investorKey, investorSecret,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

        var expectedoutput = seederInvestmentTransaction.Outputs[1];
            
        Assert.True(expectedoutput.ScriptPubKey.Equals(expectedScript));
        Assert.Equal(0,expectedoutput.Value.Satoshi);
    }
    
    //[Fact] //TODO add the correct assertion for the scripts after changing AngorScripts.CreateStage
    public void SeederInvestmentTransactionCreation_addsScriptforEachStage()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        // Create the seeder 1 params
        var seederKey = new Key();
        var seedersecret = new Key();

        var investorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes());
        var investorSecret = Hashes.Hash256(seedersecret.ToBytes()).ToString();
        // create the investment transaction

        var seederInvestmentTransaction = _sut.CreateInvestmentTransaction(projectInvestmentInfo, investorKey, investorSecret,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

        for (int i = 0; i < seederInvestmentTransaction.Outputs.Count(); i++)
        {
            var expectedoutput = seederInvestmentTransaction.Outputs[i];
            
            Assert.NotNull(expectedoutput);
        }
    }

    [Fact]
    public void BuildRecoverSeederFundsTransactions_AllTransactionsReturnedValid()
    {
        var changeAddress = new Key();
        var investmentTrxHex = "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";
        
        var investmentTrx = Networks.Bitcoin.Testnet().Consensus.ConsensusFactory.CreateTransaction(investmentTrxHex);

        var recoveryTransactions = _sut.BuildRecoverSeederFundsTransactions(investmentTrx, DateTime.Now.AddMonths(6), 
            Encoders.Hex.EncodeData(changeAddress.PubKey.ToBytes()));

        //All inputs are from the investment transaction outputs
        Assert.Contains(recoveryTransactions.SelectMany(_ => _.Inputs.AsIndexedInputs()),
            _ => investmentTrx.Outputs.AsIndexedOutputs().Any(o => o.ToOutPoint().Equals(_.PrevOut)));
    }
}
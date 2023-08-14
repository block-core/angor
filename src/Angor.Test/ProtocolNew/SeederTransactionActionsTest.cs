using Angor.Shared.Models;
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
}
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.DataEncoders;
using Moq;

namespace Angor.Test.Protocol.TransactionBuilders;

public class InvestmentTransactionBuilderTest : AngorTestData
{
    private InvestmentTransactionBuilder _sut;

    private readonly Mock<IProjectScriptsBuilder> _projectScriptsBuilder;
    private readonly Mock<IInvestmentScriptBuilder> _investmentScriptBuilder;
    public InvestmentTransactionBuilderTest()
    {
        _projectScriptsBuilder = new Mock<IProjectScriptsBuilder>();

        _investmentScriptBuilder = new Mock<IInvestmentScriptBuilder>();
        
        _sut = new InvestmentTransactionBuilder(_networkConfiguration.Object,
            _projectScriptsBuilder.Object,
            _investmentScriptBuilder.Object,
            new TaprootScriptBuilder());
    }
    
    private Script GivenTheAngorFeeScript(ProjectInfo projectInvestmentInfo)
    {
        var expectedScript = new Key().ScriptPubKey;

        _projectScriptsBuilder.Setup(_ => _.GetAngorFeeOutputScript(projectInvestmentInfo.ProjectIdentifier))
            .Returns(expectedScript);
        return expectedScript;
    }

    [Fact]
    public void SeederInvestmentTransactionCreation_addsAngorKeyScript()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        var expectedScript = GivenTheAngorFeeScript(projectInvestmentInfo);

        var opReturnScript = new Key().ScriptPubKey;

        var stageScripts = new List<ProjectScripts>()
        {
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() },
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() },
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() }
        };

        var seederInvestmentTransaction = _sut.BuildInvestmentTransaction(projectInvestmentInfo, opReturnScript,
            stageScripts, projectInvestmentInfo.TargetAmount);

        var expectedoutput = seederInvestmentTransaction.Outputs.First();

        Assert.True(expectedoutput.ScriptPubKey.Equals(expectedScript));
        Assert.Equal(projectInvestmentInfo.TargetAmount / 100, expectedoutput.Value.Satoshi);
    }

    [Fact]
    public void SeederInvestmentTransactionCreation_addsOpReturnWithProjectData()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        var opReturnScript = new Key().ScriptPubKey;

        GivenTheAngorFeeScript(projectInvestmentInfo);

        var stageScripts = new List<ProjectScripts>()
        {
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() },
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() },
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() }
        };

        var seederInvestmentTransaction = _sut.BuildInvestmentTransaction(projectInvestmentInfo, opReturnScript, 
            stageScripts, projectInvestmentInfo.TargetAmount);

        var expectedoutput = seederInvestmentTransaction.Outputs[1];
            
        Assert.True(expectedoutput.ScriptPubKey.Equals(opReturnScript));
        Assert.Equal(0,expectedoutput.Value.Satoshi);
    }
    
    [Fact]
    public void SeederInvestmentTransactionCreation_addsScriptForEachStage()
    {
        var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

        var projectInvestmentInfo = GivenValidProjectInvestmentInfo(words);

        GivenTheAngorFeeScript(projectInvestmentInfo);

        var opReturnScript = new Key().ScriptPubKey;

        var stageScripts = new List<ProjectScripts>()
        {
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() },
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() },
            new() { Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() }
        };
        
        var seederInvestmentTransaction = _sut.BuildInvestmentTransaction(projectInvestmentInfo, opReturnScript, 
            stageScripts, projectInvestmentInfo.TargetAmount);

        for (int i = 0; i < seederInvestmentTransaction.Outputs.Count(); i++)
        {
            var expectedOutput = seederInvestmentTransaction.Outputs[i];
            
            Assert.NotNull(expectedOutput.ScriptPubKey); //TODO add the correct assertion for the scripts after changing AngorScripts.CreateStage
        }

        Assert.Equal(seederInvestmentTransaction.Outputs[2].Value.Satoshi, 297000);
        Assert.Equal(seederInvestmentTransaction.Outputs[3].Value.Satoshi, 1485000);
        Assert.Equal(seederInvestmentTransaction.Outputs[4].Value.Satoshi, 295218000);
    }

    [Fact]
    public void BuildRecoverSeederFundsTransactions_AllTransactionsReturnedWithStageOutputssSpent()
    {
        var changeAddress = new Key();
        var investmentTrxHex = "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";
        
        var investmentTrx = Networks.Bitcoin.Testnet().Consensus.ConsensusFactory.CreateTransaction(investmentTrxHex);

        _investmentScriptBuilder.Setup(_ => _.GetInvestorPenaltyTransactionScript(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(new Key().ScriptPubKey);

        var recoveryTransaction = _sut.BuildUpfrontRecoverFundsTransaction(new ProjectInfo { Stages = new List<Stage> { new Stage(), new Stage(), new Stage() } }, investmentTrx, 180,
            Encoders.Hex.EncodeData(changeAddress.PubKey.ToBytes()));

        //All inputs are from the investment transaction outputs
        Assert.Contains(recoveryTransaction.Inputs.AsIndexedInputs(),
            _ => investmentTrx.Outputs.AsIndexedOutputs().Any(o => o.ToOutPoint().Equals(_.PrevOut)));
    }
    
    [Fact]
    public void BuildRecoverSeederFundsTransactions_AllTransactionsReturnedWithTheRightScript()
    {
        var changeAddress = new Key();
        var investmentTrxHex = "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";
        
        var investmentTrx = Networks.Bitcoin.Testnet().Consensus.ConsensusFactory.CreateTransaction(investmentTrxHex);

        var expectedAddress = Encoders.Hex.EncodeData(changeAddress.PubKey.ToBytes());
        var expectedDays = 180;
        var expectedScript = new Key().ScriptPubKey;
        
        _investmentScriptBuilder.Setup(_ => _.GetInvestorPenaltyTransactionScript(expectedAddress, expectedDays))
            .Returns(expectedScript);

        var recoveryTransaction = _sut.BuildUpfrontRecoverFundsTransaction(new ProjectInfo { Stages = new List<Stage> { new Stage(), new Stage(), new Stage() } }, investmentTrx, 
            expectedDays, expectedAddress);
        
        //All outputs pay to the penalty script
        Assert.Contains(recoveryTransaction.Outputs,
            _ => _.ScriptPubKey.ToHex().Equals(expectedScript.WitHash.ScriptPubKey.ToHex()));
    }
}
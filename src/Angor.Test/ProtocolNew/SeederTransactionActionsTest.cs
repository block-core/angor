using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.ProtocolNew;

public class SeederTransactionActionsTest : AngorTestData
{
    private SeederTransactionActions _sut;

    private readonly Mock<IProjectScriptsBuilder> _projectScriptsBuilder;
    private readonly Mock<IInvestmentTransactionBuilder> _investmentTransactionBuilder;
    private readonly Mock<IInvestmentScriptBuilder> _investmentScriptBuilder;

    public SeederTransactionActionsTest()
    {
        _projectScriptsBuilder = new Mock<IProjectScriptsBuilder>();

        _investmentScriptBuilder = new Mock<IInvestmentScriptBuilder>();

        _investmentTransactionBuilder = new Mock<IInvestmentTransactionBuilder>();
        
        _sut = new SeederTransactionActions(new NullLogger<SeederTransactionActions>(),
            _investmentScriptBuilder.Object, 
            _projectScriptsBuilder.Object,
            new SpendingTransactionBuilder(
                NetworkConfiguration.Object,
                _projectScriptsBuilder.Object,
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
            _investmentTransactionBuilder.Object,
            new TaprootScriptBuilder(),
            NetworkConfiguration.Object);
    }

    [Fact]
    public void SeederInvestmentTransactionCreation_CallsBuildWithSeederOpReturn()
        {
            var projectInvestmentInfo = GivenValidProjectInvestmentInfo();

            var investorKey = Encoders.Hex.EncodeData( new Key().PubKey.ToBytes());
            var investorSecret = Hashes.Hash256( new Key().ToBytes());
            // create the investment transaction

            var expectedOpReturnScript = new Key().ScriptPubKey;

            _projectScriptsBuilder.Setup(_ => _.BuildSeederInfoScript(investorKey,investorSecret))
                .Returns(expectedOpReturnScript);

            var expectedTransaction = new Transaction { Inputs = { new TxIn(new Key().ScriptPubKey) } };
            
            _investmentTransactionBuilder.Setup(_ => _.BuildInvestmentTransaction(It.IsAny<ProjectInfo>(), expectedOpReturnScript,
                    It.IsAny<IEnumerable<ProjectScripts>>(), It.IsAny<long>()))
                .Returns(expectedTransaction);

            var seederInvestmentTransaction = _sut.CreateInvestmentTransaction(projectInvestmentInfo, investorKey, investorSecret,
                Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            Assert.Same(expectedTransaction,seederInvestmentTransaction);
        }
    
    
    [Fact]
    public void SeederInvestmentTransactionCreation_CallsBuildWithSeederScriptStages()
    {
        var projectInvestmentInfo = GivenValidProjectInvestmentInfo();

        var investorKey = Encoders.Hex.EncodeData(new Key().PubKey.ToBytes());
        var investorSecret = Hashes.Hash256(new Key().ToBytes());
        // create the investment transaction
        
        var expectedProjectScripts = new ProjectScripts{ Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey, Seeders = new List<Script>() };

        var projectScriptList = projectInvestmentInfo.Stages.Select(_ => expectedProjectScripts)
            .ToList();

        _investmentScriptBuilder.Setup(_ => _.BuildProjectScriptsForStage(projectInvestmentInfo, investorKey,
            It.Is<int>(_ => _ < projectInvestmentInfo.Stages.Count), investorSecret
        )).Returns(expectedProjectScripts);
        
        var expectedTransaction = new Transaction { Inputs = { new TxIn(new Key().ScriptPubKey) } };
        
        _investmentTransactionBuilder.Setup(_ => _.BuildInvestmentTransaction(It.IsAny<ProjectInfo>(), It.IsAny<Script>(),
                projectScriptList, It.IsAny<long>()))
            .Returns(expectedTransaction);

        var seederInvestmentTransaction = _sut.CreateInvestmentTransaction(projectInvestmentInfo, investorKey, investorSecret,
            Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

        Assert.Same(expectedTransaction,seederInvestmentTransaction);
    }

    [Fact]
    public void BuildRecoverSeederFundsTransactions_CallsBuildUpfrontRecoverFundsTransactions()
    {
        var changeAddress = new Key();
        var investmentTrxHex = "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";
        
        var investmentTrx = Networks.Bitcoin.Testnet().Consensus.ConsensusFactory.CreateTransaction(investmentTrxHex);
        var penaltyDays = 180;
        var receiveAddress = Encoders.Hex.EncodeData(changeAddress.PubKey.ToBytes());
        var newProject = new ProjectInfo();

        var recoveryTransactions = _sut.BuildRecoverSeederFundsTransaction(newProject, investmentTrx, penaltyDays, 
            receiveAddress);
        
        _investmentTransactionBuilder.Verify(_ => _.BuildUpfrontRecoverFundsTransaction(newProject, investmentTrx,penaltyDays,receiveAddress),
            Times.Once);
    }
}
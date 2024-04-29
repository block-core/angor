using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Moq;
using NBitcoin;
using Key = Blockcore.NBitcoin.Key;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Test.ProtocolNew.TransactionBuilders;

public class SpendingTransactionBuilderTest : AngorTestData
{
    private SpendingTransactionBuilder _sut;

    public SpendingTransactionBuilderTest()
    {
        _sut = new SpendingTransactionBuilder(_networkConfiguration.Object,
            new ProjectScriptsBuilder(_derivationOperations),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()));
    }

    private WitScript GetRandomDataWitScript(int opListLength)
    {
        var list = Enumerable.Range(0, opListLength)
            .Select(_ => Op.GetPushOp(new Key().ScriptPubKey.ToBytes()));
        
        return new WitScript(list.ToArray());
    }
    
     [Fact]
    public void BuildRecoverSeederFundsTransactions_AllTransactionsReturnedSpendingStageOutput()
    {
        var changeAddress = new Key();
        var investmentTrxHex = "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";
        
        var investmentTrx = Networks.Bitcoin.Testnet().Consensus.ConsensusFactory.CreateTransaction(investmentTrxHex);

        var projectInfo = GivenValidProjectInvestmentInfo();

        var recoveryTransaction = _sut.BuildRecoverInvestorRemainingFundsInProject(investmentTrxHex,
            projectInfo,
            1,
            changeAddress.ScriptPubKey.WitHash.GetAddress(Networks.Bitcoin.Testnet()).ToString(),
            Encoders.Hex.EncodeData(new Key().ToBytes()),
            new NBitcoin.FeeRate(new NBitcoin.Money(Random.Shared.Next())),
            _ => { return GetRandomDataWitScript(3); },
            (_, s) => { return GetRandomDataWitScript(3); }
        );

        //All inputs are from the investment transaction outputs
        Assert.Contains(recoveryTransaction.Transaction.Inputs.AsIndexedInputs(),
            _ => investmentTrx.Outputs.AsIndexedOutputs().Any(o => o.ToOutPoint().Equals(_.PrevOut)));
    }
    
     [Fact]
    public void BuildRecoverSeederFundsTransactions_AllTransactionSetCreateAndSignWithScript()
    {
        var changeAddress = new Key();
        var investmentTrxHex = "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";

        var projectInfo = GivenValidProjectInvestmentInfo();

        var expectedWitSigWithFakeSig = GetRandomDataWitScript(3);
        var expectedFinalWitSig = GetRandomDataWitScript(3);

         _sut.BuildRecoverInvestorRemainingFundsInProject(investmentTrxHex,
            projectInfo,
            1,
            changeAddress.ScriptPubKey.WitHash.GetAddress(Networks.Bitcoin.Testnet()).ToString(),
            Encoders.Hex.EncodeData(new Key().ToBytes()),
            new NBitcoin.FeeRate(new NBitcoin.Money(Random.Shared.Next())),
            _ => { return expectedWitSigWithFakeSig; },
            (_, s) =>
            {
                Assert.NotNull(s);
                Assert.NotEmpty(s.ToBytes());
                Assert.Equal(_, expectedWitSigWithFakeSig);
                return expectedFinalWitSig;
            }
        );
    }

    [Fact]
    public void BuildRecoverSeederFundsTransactions_AllTransactionsReturnedWithTheSignwsWitscript()
    {
        var changeAddress = new Key();
        var investmentTrxHex =
            "010000000102df7eb0603e53da8760b6037cca974550e85489d36d59efcedb8834bb691a71000000006a473044022047870ec27da9e51c3f4657b6ac376bb240191b93b1c597775f092a3c6e20e66602203f544e972f45e796730c02942393f9b7d02b4d6dc2301281c68029e693194549012103d418fd52b6bd5fc9330c51aa4480aa9f3bba2940bedc7ce7535ac164aebc25efffffffff06c0c62d0000000000160014b81698f9e2e78fa7fc87f8009183c8a4ab25a6c70000000000000000446a2103eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed7720fcdcd57c6c65b40bcdf9b454a96891d7375a60d516e3416af61f86d4999d44e180c3c901000000002251204f3edc853deba516c82aa9479daaddbe5c34bdde1b2a7be369d784e560271123804a5d0500000000225120d2f4094dc5c80bee991b76b089f230f086edfcef20550467026f80e79647b3f900879303000000002251208ea1c42515559fd53f811b333be518484aeecf9409aefeccb8e977b43ae1d9c99c547e6d00000000160014333a905154f56ef18b6f7aee53ed45a231da54f700000000";

        var projectInfo = GivenValidProjectInvestmentInfo();

        var secretHash = uint256.Parse("e1449d99d4861ff66a41e316d5605a37d79168a954b4f9cd0bb4656c7cd5dcfc");
        var investorKey = "03eb7d47c80390672435987b9a7ecaa22730cd9c4537fc8d257417fb058248ed77";
        var expectedProjectScripts = new ProjectScripts
        {
            Founder = new Key().ScriptPubKey, Recover = new Key().ScriptPubKey, EndOfProject = new Key().ScriptPubKey
        };

        var mock = new Mock<IInvestmentScriptBuilder>();

        mock.Setup(_ =>
            _.BuildProjectScriptsForStage(projectInfo, investorKey, It.Is<int>(_ => _ < projectInfo.Stages.Count),
                secretHash
            )).Returns(expectedProjectScripts);
        
        var expectedFinalWitSig = GetRandomDataWitScript(3);

        _sut = new SpendingTransactionBuilder(_networkConfiguration.Object,
            new ProjectScriptsBuilder(_derivationOperations),
            mock.Object);

        var recoveryTransaction = _sut.BuildRecoverInvestorRemainingFundsInProject(investmentTrxHex,
            projectInfo,
            1,
            changeAddress.ScriptPubKey.WitHash.GetAddress(Networks.Bitcoin.Testnet()).ToString(),
            Encoders.Hex.EncodeData(new Key().ToBytes()),
            new NBitcoin.FeeRate(new NBitcoin.Money(Random.Shared.Next())),
            _ =>
            {
                Assert.Same(_, expectedProjectScripts);
                return GetRandomDataWitScript(3);
            },
            (_, s) => { return expectedFinalWitSig; }
        );

        Assert.Equivalent(recoveryTransaction.Transaction.Inputs.Select(_ => _.WitScript.ToString()),
            new List<string>()
                { expectedFinalWitSig.ToString(), expectedFinalWitSig.ToString(), expectedFinalWitSig.ToString() });
    }
}
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using Op = Blockcore.Consensus.ScriptInfo.Op;
using uint256 = Blockcore.NBitcoin.uint256;
using WitScript = Blockcore.Consensus.TransactionInfo.WitScript;

namespace Angor.Shared.ProtocolNew;

public class SeederTransactionActions : ISeederTransactionActions
{
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly ISpendingTransactionBuilder _spendingTransactionBuilder;
    private readonly IInvestmentTransactionBuilder _investmentTransactionBuilder;
    private readonly ITaprootScriptBuilder _taprootScriptBuilder;
    private readonly INetworkConfiguration _networkConfiguration;
    
    public SeederTransactionActions(IInvestmentScriptBuilder investmentScriptBuilder, IProjectScriptsBuilder projectScriptsBuilder, 
        ISpendingTransactionBuilder spendingTransactionBuilder, IInvestmentTransactionBuilder investmentTransactionBuilder, ITaprootScriptBuilder taprootScriptBuilder, INetworkConfiguration networkConfiguration)
    {
        _investmentScriptBuilder = investmentScriptBuilder;
        _projectScriptsBuilder = projectScriptsBuilder;
        _spendingTransactionBuilder = spendingTransactionBuilder;
        _investmentTransactionBuilder = investmentTransactionBuilder;
        _taprootScriptBuilder = taprootScriptBuilder;
        _networkConfiguration = networkConfiguration;
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey,
        uint256 investorSecretHash, long totalInvestmentAmount)
    {
        // create the output and script of the investor pubkey script opreturn
        var opreturnScript = _projectScriptsBuilder.BuildSeederInfoScript(investorKey, investorSecretHash);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = projectInfo.Stages
            .Select((_,index) => _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo,
                investorKey,index, investorSecretHash));

        return _investmentTransactionBuilder.BuildInvestmentTransaction(projectInfo, opreturnScript, stagesScript,
            totalInvestmentAmount);
    }
    
    public Transaction BuildRecoverSeederFundsTransaction(Transaction investmentTransaction, DateTime penaltyDate,
        string investorKey)
    {
        return _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(investmentTransaction, penaltyDate,
            investorKey);
    }

    public Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction,
        string receiveAddress, List<string> founderSignatures, string privateKey, string? secret)
    {
        var transaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(investmentTransaction, projectInfo.PenaltyDate,
            receiveAddress);

        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs[1].ScriptPubKey);
        
        var nBitcoinTransaction = NBitcoin.Transaction.Parse(transaction.ToHex(), 
            NetworkMapper.Map(_networkConfiguration.GetNetwork()));

        var outputs = investmentTransaction.Outputs.AsIndexedOutputs()
            .Where(_ => _.N > 1)
            .Select(blockcoreTxOut => new TxOut(
                    new Money(blockcoreTxOut.TxOut.Value.Satoshi),
                    new Script(blockcoreTxOut.TxOut.ScriptPubKey.ToBytes())))
            .ToArray();
        
        var key = new Key(Encoders.Hex.DecodeData(privateKey));
        var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;
        
        for (var stageIndex = 0; stageIndex < nBitcoinTransaction.Outputs.Count; stageIndex++)
        {
            var projectScripts = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, secretHash);

            var controlBlock = _taprootScriptBuilder.CreateControlBlock(projectScripts, _ => _.Recover);

            var hash = nBitcoinTransaction.GetSignatureHashTaproot(outputs,
                new TaprootExecutionData(
                        stageIndex, 
                        new NBitcoin.Script(projectScripts.Recover.ToBytes()).TaprootV1LeafHash)
                    { SigHash = sigHash });

            var investorSignature = key.SignTaprootKeySpend(hash, sigHash);

            transaction.Inputs[stageIndex].WitScript = new WitScript(
                    Op.GetPushOp(new Key(Encoders.Hex.DecodeData(secret)).ToBytes()),
                            Op.GetPushOp(investorSignature.ToBytes()),
                            Op.GetPushOp(TaprootSignature.Parse(founderSignatures[stageIndex]).ToBytes()),

                            Op.GetPushOp(projectScripts.Recover.ToBytes()),
                            Op.GetPushOp(controlBlock.ToBytes()));
        }

        return transaction;
    }

    public Transaction RecoverEndOfProjectFunds(string investmentTransactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress,
        string investorPrivateKey, FeeEstimation feeEstimation)
    {
        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(investmentTransactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            _ =>
            {
                var controlBlock = _taprootScriptBuilder.CreateControlBlock(_, script => script.EndOfProject);
                var fakeSig = new byte[64];
                return new NBitcoin.WitScript(
                    new WitScript(Op.GetPushOp(fakeSig), Op.GetPushOp(_.EndOfProject.ToBytes()),
                        Op.GetPushOp(controlBlock.ToBytes())).ToBytes());
            },
            (witScript, sig) =>
            {
                var scriptToExecute = witScript[1];
                var controlBlock = witScript[2];

                return new NBitcoin.WitScript(
                    new WitScript(Op.GetPushOp(sig.ToBytes()), Op.GetPushOp(scriptToExecute),
                        Op.GetPushOp(controlBlock)).ToBytes());
            });
    }
}
using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Utilities;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Op = Blockcore.Consensus.ScriptInfo.Op;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using uint256 = Blockcore.NBitcoin.uint256;
using WitScript = Blockcore.Consensus.TransactionInfo.WitScript;

namespace Angor.Shared.Protocol;

public class SeederTransactionActions : ISeederTransactionActions
{
    private readonly ILogger<SeederTransactionActions> _logger;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly ISpendingTransactionBuilder _spendingTransactionBuilder;
    private readonly IInvestmentTransactionBuilder _investmentTransactionBuilder;
    private readonly ITaprootScriptBuilder _taprootScriptBuilder;
    private readonly INetworkConfiguration _networkConfiguration;
    
    public SeederTransactionActions(ILogger<SeederTransactionActions> logger, IInvestmentScriptBuilder investmentScriptBuilder, IProjectScriptsBuilder projectScriptsBuilder, 
        ISpendingTransactionBuilder spendingTransactionBuilder, IInvestmentTransactionBuilder investmentTransactionBuilder, ITaprootScriptBuilder taprootScriptBuilder, INetworkConfiguration networkConfiguration)
    {
        _logger = logger;
        _investmentScriptBuilder = investmentScriptBuilder;
        _projectScriptsBuilder = projectScriptsBuilder;
        _spendingTransactionBuilder = spendingTransactionBuilder;
        _investmentTransactionBuilder = investmentTransactionBuilder;
        _taprootScriptBuilder = taprootScriptBuilder;
        _networkConfiguration = networkConfiguration;
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, uint256 investorSecretHash, long totalInvestmentAmount)
    {
        return CreateInvestmentTransaction(projectInfo, FundingParameters.CreateForInvest(projectInfo, investorKey, totalInvestmentAmount, investorSecretHash));
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, FundingParameters parameters)
    {
        var opreturnScript = _projectScriptsBuilder.BuildSeederInfoScript(projectInfo, parameters);

        var stageCount = ProjectParametersHelper.GetStageCount(projectInfo, parameters);

        List<ProjectScripts> stagesScript = Enumerable.Range(0, stageCount).Select(index =>
           _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, parameters, index)).ToList();

        return _investmentTransactionBuilder.BuildInvestmentTransaction(projectInfo, opreturnScript, stagesScript,
            parameters.TotalInvestmentAmount);
    }
    
    public Transaction BuildRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, int penaltyDays,
        string investorKey)
    {
        return _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, penaltyDays,
            investorKey);
    }

    public Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction,
        string receiveAddress, SignatureInfo founderSignatures, string privateKey, string? secret)
    {
        var recoveryTransaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDays, receiveAddress);

        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);
        
        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, investmentTransaction);

        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Where(txout => txout.TxOut.ScriptPubKey.IsScriptType(NBitcoin.ScriptType.Taproot))
            .Select(_ => _.TxOut)
            .ToArray();

        var key = new Key(Encoders.Hex.DecodeData(privateKey));
        var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        for (var stageIndex = 0; stageIndex < projectInfo.Stages.Count; stageIndex++)
        {
            var projectScripts = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageIndex);

            var controlBlock = _taprootScriptBuilder.CreateControlBlock(projectScripts, _ => _.Recover);

            var tapScript = new NBitcoin.Script(projectScripts.Recover.ToBytes()).ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(stageIndex, tapScript.LeafHash) { SigHash = sigHash };
            var hash = nbitcoinRecoveryTransaction.GetSignatureHashTaproot(outputs, execData);

            _logger.LogInformation($"project={projectInfo.ProjectIdentifier}; seeder-pubkey={key.PubKey.ToHex()}; stage={stageIndex}; hash={hash}");

            var investorSignature = key.SignTaprootKeySpend(hash, sigHash);

            recoveryTransaction.Inputs[stageIndex].WitScript = new WitScript(
                   Op.GetPushOp(new Key(Encoders.Hex.DecodeData(secret)).ToBytes()),
                            Op.GetPushOp(investorSignature.ToBytes()),
                            Op.GetPushOp(TaprootSignature.Parse(founderSignatures.Signatures.First(f => f.StageIndex == stageIndex).Signature).ToBytes()),
                            Op.GetPushOp(projectScripts.Recover.ToBytes()),
                            Op.GetPushOp(controlBlock.ToBytes()));
        }

        return recoveryTransaction;
    }

    public TransactionInfo RecoverEndOfProjectFunds(string investmentTransactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress,
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
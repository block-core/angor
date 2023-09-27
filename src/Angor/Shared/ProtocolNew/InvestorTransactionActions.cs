using Angor.Shared.Models;
//using Angor.Shared.Protocol;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using System;
using Microsoft.Extensions.Logging;
using Key = Blockcore.NBitcoin.Key;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using System.Reflection;

namespace Angor.Shared.ProtocolNew;

public class InvestorTransactionActions : IInvestorTransactionActions
{
    private readonly ILogger<InvestorTransactionActions> _logger;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly ISpendingTransactionBuilder _spendingTransactionBuilder;
    private readonly IInvestmentTransactionBuilder _investmentTransactionBuilder;
    private readonly ITaprootScriptBuilder _taprootScriptBuilder;
    private readonly INetworkConfiguration _networkConfiguration;

    public InvestorTransactionActions(ILogger<InvestorTransactionActions> logger, IInvestmentScriptBuilder investmentScriptBuilder, IProjectScriptsBuilder projectScriptsBuilder, ISpendingTransactionBuilder spendingTransactionBuilder, IInvestmentTransactionBuilder investmentTransactionBuilder, ITaprootScriptBuilder taprootScriptBuilder, INetworkConfiguration networkConfiguration)
    {
        _logger = logger;
        _investmentScriptBuilder = investmentScriptBuilder;
        _projectScriptsBuilder = projectScriptsBuilder;
        _spendingTransactionBuilder = spendingTransactionBuilder;
        _investmentTransactionBuilder = investmentTransactionBuilder;
        _taprootScriptBuilder = taprootScriptBuilder;
        _networkConfiguration = networkConfiguration;
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey,
        long totalInvestmentAmount)
    {
        // create the output and script of the investor pubkey script opreturn
        var opreturnScript = _projectScriptsBuilder.BuildInvestorInfoScript(investorKey);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = Enumerable.Range(0,projectInfo.Stages.Count)
            .Select(index => _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, index));

        return _investmentTransactionBuilder.BuildInvestmentTransaction(projectInfo, opreturnScript, stagesScript,
            totalInvestmentAmount);
    }

    public string DiscoverUsedScript(ProjectInfo projectInfo, Transaction investmentTransaction, int stageIndex, string witScript)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var scripts = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex);

        var witScriptInfo = new Blockcore.Consensus.TransactionInfo.WitScript(Blockcore.Consensus.ScriptInfo.Script.FromHex(witScript));
        var executeScript = new Blockcore.Consensus.ScriptInfo.Script(witScriptInfo[witScriptInfo.PushCount - 2]);

        var withex = executeScript.ToHex();

        // todo: turn this to eunms perhaps?
        if (withex == scripts.Founder.ToHex())
        {
            return "Founder";
        }

        if (withex == scripts.Recover.ToHex())
        {
            return $"Penalty, locked for {(projectInfo.PenaltyDate - DateTime.Now).Days} days";
        }

        if (withex == scripts.EndOfProject.ToHex())
        {
            return "Investor";
        }

        return "unknown";
    }

    public Transaction BuildRecoverInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        return _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDate, investorKey);
    }

    public Transaction RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex,
        string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation)
    {
        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new NBitcoin.FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            projectScripts =>
            {
                var controlBlock = _taprootScriptBuilder.CreateControlBlock(projectScripts, _ => _.EndOfProject);
                var fakeSig = new byte[64];
                return new NBitcoin.WitScript(NBitcoin.Op.GetPushOp(fakeSig),
                    NBitcoin.Op.GetPushOp(projectScripts.EndOfProject.ToBytes()),
                    NBitcoin.Op.GetPushOp(controlBlock.ToBytes()));
            },
            (witScript, sig) =>
            {
                var scriptToExecute = witScript[1];
                var controlBlock = witScript[2];

                return new NBitcoin.WitScript(NBitcoin.Op.GetPushOp(sig.ToBytes()),
                    NBitcoin.Op.GetPushOp(scriptToExecute), NBitcoin.Op.GetPushOp(controlBlock));
            });
    }

    public Transaction RecoverRemainingFundsWithOutPenalty(string transactionHex, ProjectInfo projectInfo, int stageIndex,
        string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation,
        IEnumerable<byte[]> seederSecrets)
    {
        var secrets = seederSecrets.Select(_ => new Key(_));
        
        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new NBitcoin.FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            _ =>
            {
                var result = _taprootScriptBuilder.CreateControlSeederSecrets(_,   projectInfo.ProjectSeeders.Threshold,secrets.ToArray());

                // use fake data for fee estimation
                var fakeSig = new byte[64];

                List<Op> ops = new List<Op>();

                ops.Add(Op.GetPushOp(fakeSig));

                foreach (var secret in result.secrets.Reverse())
                {
                    ops.Add(Op.GetPushOp(secret.ToBytes()));
                }

                ops.Add(Op.GetPushOp(result.execute.ToBytes()));
                ops.Add(Op.GetPushOp(result.controlBlock.ToBytes()));

                return new NBitcoin.WitScript(ops.ToArray());
            },
            (witScript, sig) =>
            {
                var controBlock = new NBitcoin.Script(witScript[witScript.PushCount - 1]);
                var scriptToExecute = new NBitcoin.Script(witScript[witScript.PushCount - 2]);
                
                List<Op> ops = new List<Op>();

                // the last 3 items on the stack are the fakesig, script and controlblock anything before that is the secrets

                ops.Add(Op.GetPushOp(sig.ToBytes()));

                foreach (var oppush  in witScript.Pushes.Skip(1).Take(witScript.Pushes.Count() - 3))
                {
                    ops.Add(Op.GetPushOp(oppush));
                }

                ops.Add(Op.GetPushOp(scriptToExecute.ToBytes()));
                ops.Add(Op.GetPushOp(controBlock.ToBytes()));

                return new NBitcoin.WitScript(ops.ToArray());
            });
    }
    
     public Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorPrivateKey)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var recoveryTransaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDate, investorKey);

        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var key = new NBitcoin.Key(Encoders.Hex.DecodeData(investorPrivateKey));
        var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Skip(2).Take(projectInfo.Stages.Count)
            .Select(_ => _.TxOut)
            .ToArray();

        // todo: david change to Enumerable.Range 
        for (var stageIndex = 0; stageIndex < projectInfo.Stages.Count; stageIndex++)
        {
            var scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, secretHash);
            var controlBlock = _taprootScriptBuilder.CreateControlBlock(scriptStages, _ => _.Recover);

            var execData = new TaprootExecutionData(stageIndex, new NBitcoin.Script(scriptStages.Recover.ToBytes()).TaprootV1LeafHash) { SigHash = sigHash };
            var hash = nbitcoinRecoveryTransaction.GetSignatureHashTaproot(outputs, execData);

            _logger.LogInformation($"project={projectInfo.ProjectIdentifier}; investor-pubkey={key.PubKey.ToHex()}; stage={stageIndex}; hash={hash}");

            var investorSignature = key.SignTaprootKeySpend(hash, sigHash);

            recoveryTransaction.Inputs[stageIndex].WitScript = new Blockcore.Consensus.TransactionInfo.WitScript(
                new WitScript(
                    Op.GetPushOp(investorSignature.ToBytes()),
                    Op.GetPushOp(TaprootSignature.Parse(founderSignatures.Signatures.First(f => f.StageIndex == stageIndex).Signature).ToBytes()),
                    Op.GetPushOp(scriptStages.Recover.ToBytes()),
                    Op.GetPushOp(controlBlock.ToBytes())).ToBytes());
        }

        return recoveryTransaction;
    }

     public bool CheckInvestorRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures)
     {
         var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var recoveryTransaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDate, investorKey);

        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nBitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var pubkey = new PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey();
        var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Skip(2).Take(projectInfo.Stages.Count)
            .Select(_ => _.TxOut)
            .ToArray();

        // todo: david change to Enumerable.Range 
        for (var stageIndex = 0; stageIndex < projectInfo.Stages.Count; stageIndex++)
        {
            var scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, secretHash);

            var execData = new TaprootExecutionData(stageIndex, new NBitcoin.Script(scriptStages.Recover.ToBytes()).TaprootV1LeafHash) { SigHash = sigHash };
            var hash = nBitcoinRecoveryTransaction.GetSignatureHashTaproot(outputs, execData);

            _logger.LogInformation($"project={projectInfo.ProjectIdentifier}; founder-recovery-pubkey={projectInfo.FounderRecoveryKey}; stage={stageIndex}; hash={hash}");

            var result = pubkey.VerifySignature(hash, TaprootSignature.Parse(founderSignatures.Signatures.First(f => f.StageIndex == stageIndex).Signature).SchnorrSignature);

            if (result == false)
                throw new Exception("Invalid signatures provided by founder");
        }

        return true;
     }
}
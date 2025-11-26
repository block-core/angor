using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Key = Blockcore.NBitcoin.Key;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using WitScript = NBitcoin.WitScript;
using Money = Blockcore.NBitcoin.Money;
using SigHash = Blockcore.Consensus.ScriptInfo.SigHash;
using Angor.Shared.Utilities;

namespace Angor.Shared.Protocol;

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

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount)
    {
        return CreateInvestmentTransaction(projectInfo, FundingParameters.CreateForInvest(projectInfo, investorKey, totalInvestmentAmount));
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, FundingParameters parameters)
    {
        var opreturnScript = _projectScriptsBuilder.BuildInvestorInfoScript(projectInfo, parameters);

        var stageCount = ProjectParametersHelper.GetStageCount(projectInfo, parameters);

        List<ProjectScripts> stagesScript = Enumerable.Range(0, stageCount).Select(index =>
                   _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, parameters, index)).ToList();

        return _investmentTransactionBuilder.BuildInvestmentTransaction(
             projectInfo, opreturnScript, stagesScript, parameters.TotalInvestmentAmount);
    }

    public ProjectScriptType DiscoverUsedScript(ProjectInfo projectInfo, Transaction investmentTransaction, int stageIndex, string witScript)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, investmentTransaction);

        var scripts = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageIndex);

        var witScriptInfo = new Blockcore.Consensus.TransactionInfo.WitScript(Blockcore.Consensus.ScriptInfo.Script.FromHex(witScript));
        var executeScript = new Blockcore.Consensus.ScriptInfo.Script(witScriptInfo[witScriptInfo.PushCount - 2]);

        var withex = executeScript.ToHex();

        if (withex == scripts.Founder.ToHex())
        {
            return new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.Founder };
        }

        if (withex == scripts.Recover.ToHex())
        {
            return new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.InvestorWithPenalty };
        }

        if (withex == scripts.EndOfProject.ToHex())
        {
            return new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.EndOfProject };
        }

        return new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.Unknown };
    }

    public Transaction BuildRecoverInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        return _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDays, investorKey);
    }

    public Transaction BuildUnfundedReleaseInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, string investorReleaseKey)
    {
        return _investmentTransactionBuilder.BuildUpfrontUnfundedReleaseFundsTransaction(projectInfo, investmentTransaction, investorReleaseKey);
    }

    public TransactionInfo BuildAndSignRecoverReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction,
        Transaction recoveryTransaction, string investorReceiveAddress, FeeEstimation feeEstimation, string investorPrivateKey)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var spendingScript = _investmentScriptBuilder.GetInvestorPenaltyTransactionScript(
            investorKey,
            projectInfo.PenaltyDays);

        var network = _networkConfiguration.GetNetwork();
        var transaction = network.CreateTransaction();

        transaction.Version = 2; // to trigger bip68 rules

        // add the output address
        transaction.Outputs.Add(new Blockcore.Consensus.TransactionInfo.TxOut(Money.Zero, Blockcore.NBitcoin.BitcoinAddress.Create(investorReceiveAddress, network)));

        // add all the outputs that are in a penalty
        foreach (var output in recoveryTransaction.Outputs.AsIndexedOutputs())
        {
            if (output.TxOut.ScriptPubKey == spendingScript.WitHash.ScriptPubKey)
            {
                // this is a penalty output
                var txIn = new Blockcore.Consensus.TransactionInfo.TxIn(output.ToOutPoint()) { Sequence = new Blockcore.NBitcoin.Sequence(TimeSpan.FromDays(projectInfo.PenaltyDays)) };
                transaction.Inputs.Add(txIn);

                // Set a fake WitScript (placeholder) for fee estimation
                txIn.WitScript = new Blockcore.Consensus.TransactionInfo.WitScript(
                    Blockcore.Consensus.ScriptInfo.Op.GetPushOp(new byte[64]),
                    Blockcore.Consensus.ScriptInfo.Op.GetPushOp(new byte[spendingScript.ToBytes().Length]));

                transaction.Outputs[0].Value += output.TxOut.Value;
            }
        }

        // reduce the network fee form the first output
        var virtualSize = transaction.GetVirtualSize(4);
        var fee = new Blockcore.NBitcoin.FeeRate(Blockcore.NBitcoin.Money.Satoshis(feeEstimation.FeeRate)).GetFee(virtualSize);
        transaction.Outputs[0].Value -= new Blockcore.NBitcoin.Money(fee);

        // sign the inputs (replace fake WitScript with real one)
        var key = new Key(Encoders.Hex.DecodeData(investorPrivateKey));

        foreach (var intput in transaction.Inputs)
        {
            var spendingOutput = recoveryTransaction.Outputs.AsIndexedOutputs().First(f => f.ToOutPoint() == intput.PrevOut);

            var hash = transaction.GetSignatureHash(network, new Blockcore.NBitcoin.ScriptCoin(intput.PrevOut, spendingOutput.TxOut, spendingScript));
            var sig = key.Sign(hash, SigHash.All);

            // Replace the fake WitScript with the real one
            intput.WitScript = new Blockcore.Consensus.TransactionInfo.WitScript(
                Blockcore.Consensus.ScriptInfo.Op.GetPushOp(sig.ToBytes()),
                Blockcore.Consensus.ScriptInfo.Op.GetPushOp(spendingScript.ToBytes()));
        }

        return new TransactionInfo { Transaction = transaction, TransactionFee = fee };
    }

    public TransactionInfo RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex,
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

    public TransactionInfo RecoverRemainingFundsWithOutPenalty(string transactionHex, ProjectInfo projectInfo, int stageIndex,
        string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation,
        IEnumerable<byte[]> seederSecrets)
    {
        var secrets = seederSecrets.Select(_ => new Key(_));

        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new NBitcoin.FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            _ =>
            {
                var result = _taprootScriptBuilder.CreateControlSeederSecrets(_, projectInfo.ProjectSeeders.Threshold, secrets.ToArray());

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

                foreach (var oppush in witScript.Pushes.Skip(1).Take(witScript.Pushes.Count() - 3))
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

        var unsignedRecoverTransaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDays, investorKey);

        var recoverTransaction = AddSignaturesToRecoveryPathTransaction(projectInfo, investmentTransaction, unsignedRecoverTransaction, founderSignatures, investorPrivateKey);

        return recoverTransaction;
    }

    public Transaction AddSignaturesToUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorPrivateKey, string investorReleaseKey)
    {
        var unsignedUnfundedReleaseTransaction = _investmentTransactionBuilder.BuildUpfrontUnfundedReleaseFundsTransaction(projectInfo, investmentTransaction, investorReleaseKey);

        var unfundedReleaseTransaction = AddSignaturesToRecoveryPathTransaction(projectInfo, investmentTransaction, unsignedUnfundedReleaseTransaction, founderSignatures, investorPrivateKey);

        return unfundedReleaseTransaction;
    }

    public bool CheckInvestorRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var unsignedRecoveryTransaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDays, investorKey);

        return CheckRecoverySignatures(projectInfo, investmentTransaction, unsignedRecoveryTransaction, founderSignatures);
    }

    public bool CheckInvestorUnfundedReleaseSignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorReleaseKey)
    {
        var unsignedUnfundedReleaseFundsTransaction = _investmentTransactionBuilder.BuildUpfrontUnfundedReleaseFundsTransaction(projectInfo, investmentTransaction, investorReleaseKey);

        return CheckRecoverySignatures(projectInfo, investmentTransaction, unsignedUnfundedReleaseFundsTransaction, founderSignatures);
    }

    public bool IsInvestmentAbovePenaltyThreshold(ProjectInfo projectInfo, long investmentAmount)
    {
        return PenaltyThresholdHelper.IsInvestmentAbovePenaltyThreshold(projectInfo, investmentAmount);
    }

    private DateTime? GetExpiryDateOverride(ProjectInfo projectInfo, long investmentAmount)
    {
        return PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, investmentAmount);
    }

    private Transaction AddSignaturesToRecoveryPathTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, Transaction recoveryTransaction, SignatureInfo founderSignatures, string investorPrivateKey)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var key = new NBitcoin.Key(Encoders.Hex.DecodeData(investorPrivateKey));
        var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, investmentTransaction);

        // Count Taproot outputs to determine stage count
        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Where(txout => txout.TxOut.ScriptPubKey.IsScriptType(NBitcoin.ScriptType.Taproot))
            .Select(_ => _.TxOut)
            .ToArray();

        for (var stageIndex = 0; stageIndex < outputs.Length; stageIndex++)
        {
            ProjectScripts scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageIndex); 

            var controlBlock = _taprootScriptBuilder.CreateControlBlock(scriptStages, _ => _.Recover);

            var tapScript = new NBitcoin.Script(scriptStages.Recover.ToBytes()).ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(stageIndex, tapScript.LeafHash) { SigHash = sigHash };

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

    private bool CheckRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, Transaction recoveryTransaction, SignatureInfo founderSignatures)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nBitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var pubkey = new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey();
        var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, investmentTransaction);

        // Count Taproot outputs to determine stage count
        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Where(txout => txout.TxOut.ScriptPubKey.IsScriptType(NBitcoin.ScriptType.Taproot))
            .Select(_ => _.TxOut)
            .ToArray();

        bool validationgPassed = true;
        for (var stageIndex = 0; stageIndex < outputs.Length; stageIndex++)
        {
            ProjectScripts scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageIndex);

            var tapScript = new NBitcoin.Script(scriptStages.Recover.ToBytes()).ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(stageIndex, tapScript.LeafHash) { SigHash = sigHash };
            var hash = nBitcoinRecoveryTransaction.GetSignatureHashTaproot(outputs, execData);
            var sig = founderSignatures.Signatures.First(f => f.StageIndex == stageIndex).Signature;

            var result = pubkey.VerifySignature(hash, TaprootSignature.Parse(sig).SchnorrSignature);

            _logger.LogInformation($"verifying sig for project={projectInfo.ProjectIdentifier}; success = {result}; founder-recovery-pubkey={projectInfo.FounderRecoveryKey}; stage={stageIndex}; hash={hash}; signature-hex={sig}");

            // if even one sig failed we fail all the validation
            if (result == false)
                validationgPassed = false;
        }

        return validationgPassed;
    }
}
using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;

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

        var witScriptInfo = new WitScript(Encoders.Hex.DecodeData(witScript));
        var executeScript = new Script(witScriptInfo[witScriptInfo.PushCount - 2]);

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
        Transaction recoveryTransaction, string investorReceiveAddress, FeeEstimation feeEstimation, AngorKey investorPrivateKey)
    {
        // H4: Reject fee rates below the protocol minimum — FeeRate must be in sat/kB.
        if (feeEstimation.FeeRate < ProtocolConstants.MinFeeRateSatsPerKb)
            throw new ArgumentOutOfRangeException(nameof(feeEstimation),
                $"Fee rate {feeEstimation.FeeRate} sat/kB is below the protocol minimum of {ProtocolConstants.MinFeeRateSatsPerKb} sat/kB.");

        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var spendingScript = _investmentScriptBuilder.GetInvestorPenaltyTransactionScript(
            investorKey,
            projectInfo.PenaltyDays);

        var network = _networkConfiguration.GetNetwork();
        var transaction = network.CreateTransaction();

        transaction.Version = 2; // to trigger bip68 rules

        // add the output address
        transaction.Outputs.Add(new TxOut(Money.Zero, BitcoinAddress.Create(investorReceiveAddress, network.BitcoinNetwork)));

        // add all the outputs that are in a penalty
        foreach (var output in recoveryTransaction.Outputs.AsIndexedOutputs())
        {
            if (output.TxOut.ScriptPubKey == spendingScript.WitHash.ScriptPubKey)
            {
                // this is a penalty output
                var txIn = new TxIn(new OutPoint(output.Transaction, output.N)) { Sequence = new Sequence(TimeSpan.FromDays(projectInfo.PenaltyDays)) };
                transaction.Inputs.Add(txIn);

                // Set a fake WitScript (placeholder) for fee estimation
                txIn.WitScript = new WitScript(
                    Op.GetPushOp(new byte[64]),
                    Op.GetPushOp(new byte[spendingScript.ToBytes().Length]));

                transaction.Outputs[0].Value += output.TxOut.Value;
            }
        }

        // reduce the network fee form the first output
        var virtualSize = transaction.GetVirtualSize();
        var fee = new FeeRate(Money.Satoshis(feeEstimation.FeeRate)).GetFee(virtualSize);
        transaction.Outputs[0].Value -= fee;

        // sign the inputs (replace fake WitScript with real one)
        var keyBytes = investorPrivateKey.ToBytes();
        Key key;
        try
        {
            key = new Key(keyBytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(keyBytes);
        }

        int inputIndex = 0;
        foreach (var intput in transaction.Inputs)
        {
            var spendingOutput = recoveryTransaction.Outputs.AsIndexedOutputs().First(f => new OutPoint(f.Transaction, f.N) == intput.PrevOut);

            var hash = transaction.GetSignatureHash(spendingScript, inputIndex, SigHash.All, spendingOutput.TxOut, HashVersion.WitnessV0);
            var sig = new TransactionSignature(key.Sign(hash), SigHash.All);

            // Replace the fake WitScript with the real one
            intput.WitScript = new WitScript(
                Op.GetPushOp(sig.ToBytes()),
                Op.GetPushOp(spendingScript.ToBytes()));

            inputIndex++;
        }

        return new TransactionInfo { Transaction = transaction, TransactionFee = fee.Satoshi };
    }

    public TransactionInfo RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int startStageNumber,
        string investorReceiveAddress, AngorKey investorPrivateKey, FeeEstimation feeEstimation)
    {
        // H4: Reject fee rates below the protocol minimum
        if (feeEstimation.FeeRate < ProtocolConstants.MinFeeRateSatsPerKb)
            throw new ArgumentOutOfRangeException(nameof(feeEstimation),
                $"Fee rate {feeEstimation.FeeRate} sat/kB is below the protocol minimum of {ProtocolConstants.MinFeeRateSatsPerKb} sat/kB.");

        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, startStageNumber,
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

    public TransactionInfo RecoverRemainingFundsWithOutPenalty(string transactionHex, ProjectInfo projectInfo, int startStageNumber,
        string investorReceiveAddress, AngorKey investorPrivateKey, FeeEstimation feeEstimation,
        IEnumerable<byte[]> seederSecrets)
    {
        // H4: Reject fee rates below the protocol minimum
        if (feeEstimation.FeeRate < ProtocolConstants.MinFeeRateSatsPerKb)
            throw new ArgumentOutOfRangeException(nameof(feeEstimation),
                $"Fee rate {feeEstimation.FeeRate} sat/kB is below the protocol minimum of {ProtocolConstants.MinFeeRateSatsPerKb} sat/kB.");

        var secrets = seederSecrets.Select(_ => new Key(_));

        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, startStageNumber,
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
                if (witScript.PushCount < 3)
                    throw new InvalidOperationException($"Unexpected witness structure: expected at least 3 pushes (sig, script, controlblock), got {witScript.PushCount}");

                var controBlock = new NBitcoin.Script(witScript[witScript.PushCount - 1]);
                var scriptToExecute = new NBitcoin.Script(witScript[witScript.PushCount - 2]);

                List<Op> ops = new List<Op>();

                // Witness layout: [fakeSig] [secret_0 .. secret_n] [scriptToExecute] [controlBlock]
                // Replace fakeSig with real sig, preserve secrets, reattach script + controlblock
                var secretCount = witScript.PushCount - 3; // exclude fakeSig, script, controlblock

                ops.Add(Op.GetPushOp(sig.ToBytes()));

                for (int i = 0; i < secretCount; i++)
                {
                    ops.Add(Op.GetPushOp(witScript[i + 1])); // secrets start at index 1
                }

                ops.Add(Op.GetPushOp(scriptToExecute.ToBytes()));
                ops.Add(Op.GetPushOp(controBlock.ToBytes()));

                return new NBitcoin.WitScript(ops.ToArray());
            });
    }

    public Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, AngorKey investorPrivateKey)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var unsignedRecoverTransaction = _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransaction(projectInfo, investmentTransaction, projectInfo.PenaltyDays, investorKey);

        var recoverTransaction = AddSignaturesToRecoveryPathTransaction(projectInfo, investmentTransaction, unsignedRecoverTransaction, founderSignatures, investorPrivateKey);

        return recoverTransaction;
    }

    public Transaction AddSignaturesToUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, AngorKey investorPrivateKey, string investorReleaseKey)
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
        if (projectInfo.ProjectType != ProjectType.Fund)
        {
            return false;
        }

        return PenaltyThresholdHelper.IsInvestmentAbovePenaltyThreshold(projectInfo, investmentAmount);
    }

    private DateTime? GetExpiryDateOverride(ProjectInfo projectInfo, long investmentAmount)
    {
        return PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, investmentAmount);
    }

    private Transaction AddSignaturesToRecoveryPathTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, Transaction recoveryTransaction, SignatureInfo founderSignatures, AngorKey investorPrivateKey)
    {
        // Verify founder signatures before incorporating them
        if (!CheckRecoverySignatures(projectInfo, investmentTransaction, recoveryTransaction, founderSignatures))
            throw new InvalidOperationException("Founder recovery signature verification failed. Refusing to co-sign with invalid founder signatures.");

        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var nbitcoinNetwork = _networkConfiguration.GetNetwork().BitcoinNetwork;
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var key = new NBitcoin.Key(investorPrivateKey.ToBytes());
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

            _logger.LogDebug("Signing recovery for project={ProjectId}, stage={Stage}", projectInfo.ProjectIdentifier, stageIndex);

            var investorSignature = key.SignTaprootKeySpend(hash, sigHash);

            recoveryTransaction.Inputs[stageIndex].WitScript = new WitScript(
                    Op.GetPushOp(investorSignature.ToBytes()),
                    Op.GetPushOp(TaprootSignature.Parse(founderSignatures.Signatures.First(f => f.StageIndex == stageIndex).Signature).ToBytes()),
                    Op.GetPushOp(scriptStages.Recover.ToBytes()),
                    Op.GetPushOp(controlBlock.ToBytes()));
        }

        return recoveryTransaction;
    }

    private bool CheckRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, Transaction recoveryTransaction, SignatureInfo founderSignatures)
    {
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey);

        var nbitcoinNetwork = _networkConfiguration.GetNetwork().BitcoinNetwork;
        var nBitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var pubkey = new TaprootPubKey(
            Angor.Shared.Protocol.Scripts.TaprootKeyHelper.GetTaprootOutputKeyBytes(projectInfo.FounderRecoveryKey));
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

            _logger.LogDebug("Signature verification for project={ProjectId}, stage={Stage}: {Result}", projectInfo.ProjectIdentifier, stageIndex, result);

            // if even one sig failed we fail all the validation
            if (result == false)
                validationgPassed = false;
        }

        return validationgPassed;
    }
}

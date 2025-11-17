using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Utilities;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Diagnostics;
using static NBitcoin.Scripting.OutputDescriptor;
//using Angor.Shared.Protocol;
using IndexedTxOut = NBitcoin.IndexedTxOut;
using Key = NBitcoin.Key;
using Op = NBitcoin.Op;
using OutPoint = NBitcoin.OutPoint;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using uint256 = Blockcore.NBitcoin.uint256;
using Utils = NBitcoin.Utils;
using WitScript = NBitcoin.WitScript;

namespace Angor.Shared.Protocol;

public class FounderTransactionActions : IFounderTransactionActions
{
    private readonly ILogger<FounderTransactionActions> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly ITaprootScriptBuilder _taprootScriptBuilder;

    public FounderTransactionActions(ILogger<FounderTransactionActions> logger, INetworkConfiguration networkConfiguration, IProjectScriptsBuilder projectScriptsBuilder, IInvestmentScriptBuilder investmentScriptBuilder, ITaprootScriptBuilder taprootScriptBuilder)
    {
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _projectScriptsBuilder = projectScriptsBuilder;
        _investmentScriptBuilder = investmentScriptBuilder;
        _taprootScriptBuilder = taprootScriptBuilder;
    }

    public SignatureInfo SignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex,
        Transaction recoveryTransaction, string founderPrivateKey)
    {
        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTrxHex, nbitcoinNetwork);

        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));
        const TaprootSigHash sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var (investorKey, secretHash) = GetProjectDetailsFromOpReturn(nbitcoinInvestmentTransaction);

        // Extract dynamic stage info for Fund/Subscribe projects
        DateTime? investmentStartDate = null;
        byte patternIndex = 0;

        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            var opReturnOutput = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs().ElementAt(1);
            var dynamicStageInfo = _projectScriptsBuilder.GetDynamicStageInfoFromOpReturnScript(
                new Script(opReturnOutput.TxOut.ScriptPubKey.ToBytes()));

            if (dynamicStageInfo != null)
            {
                investmentStartDate = dynamicStageInfo.GetInvestmentStartDate();
                patternIndex = dynamicStageInfo.PatternId;
            }
        }

        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Where(txout => txout.TxOut.ScriptPubKey.IsScriptType(ScriptType.Taproot))
            .Select(_ => _.TxOut)
            .ToArray();

        SignatureInfo info = new SignatureInfo { ProjectIdentifier = projectInfo.ProjectIdentifier };

        for (var stageIndex = 0; stageIndex < outputs.Length; stageIndex++)
        {
            ProjectScripts scriptStages;

            if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
            {
                // Dynamic stages - pass investment start date and pattern index
                scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(
                    projectInfo,
                    investorKey,
                    stageIndex,
                    secretHash,
                    null, // expiryDateOverride
                    investmentStartDate,
                    patternIndex);
            }
            else
            {
                // Fixed stages - use original method
                scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(
                    projectInfo,
                    investorKey,
                    stageIndex,
                    secretHash);
            }

            var tapScript = new NBitcoin.Script(scriptStages.Recover.ToBytes()).ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(stageIndex, tapScript.LeafHash) { SigHash = sigHash };
            var hash = nbitcoinRecoveryTransaction.GetSignatureHashTaproot(outputs, execData);

            var sig = key.SignTaprootKeySpend(hash, sigHash).ToString();

            var hashHex = Encoders.Hex.EncodeData(hash.ToBytes());

            _logger.LogInformation($"creating sig for project={projectInfo.ProjectIdentifier}; founder-recovery-pubkey={key.PubKey.ToHex()}; stage={stageIndex}; hash={hash}; encodedHash={hashHex} signature-hex={sig}");

            var result = key.PubKey.GetTaprootFullPubKey().VerifySignature(hash, TaprootSignature.Parse(sig).SchnorrSignature);

            _logger.LogInformation($"verification = {result}");

            info.Signatures.Add(new SignatureInfoItem { Signature = sig, StageIndex = stageIndex });
        }

        return info;
    }

    /// <summary>
    /// Allow the founder to spend the coins in a stage after the timelock has passed
    /// </summary>
    /// <exception cref="Exception"></exception>
    public TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, int stageNumber, Script founderRecieveAddress, string founderPrivateKey,
        FeeEstimation fee)
    {
        var network = _networkConfiguration.GetNetwork();

        // We'll use the NBitcoin lib because its a taproot spend
        var nbitcoinNetwork = NetworkMapper.Map(network);

        var spendingTransaction = nbitcoinNetwork.CreateTransaction();

        // Step 1 - the time lock
        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        DateTime stageReleaseDate;

        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            // Dynamic stages - calculate release date from first investment transaction
            // All investments should have the same pattern, so we use the first one
            var firstInvestmentHex = investmentTransactionsHex.First();
            var firstInvestmentTrx = NBitcoin.Transaction.Parse(firstInvestmentHex, nbitcoinNetwork);
            var opReturnOutput = firstInvestmentTrx.Outputs.AsIndexedOutputs().ElementAt(1);

            var dynamicStageInfo = _projectScriptsBuilder.GetDynamicStageInfoFromOpReturnScript(
                new Script(opReturnOutput.TxOut.ScriptPubKey.ToBytes()));

            if (dynamicStageInfo == null)
            {
                throw new InvalidOperationException("Dynamic stage info not found in investment transaction for Fund/Subscribe project");
            }

            var investmentStartDate = dynamicStageInfo.GetInvestmentStartDate();
            var pattern = projectInfo.DynamicStagePatterns[dynamicStageInfo.PatternId];

            // Compute stages from pattern
            var computedStages = DynamicStageHelper.ComputeStagesFromPattern(pattern, investmentStartDate);

            if (stageNumber < 1 || stageNumber > computedStages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stageNumber),
                    $"Stage number {stageNumber} is out of range. Project has {computedStages.Count} stages.");
            }

            stageReleaseDate = computedStages[stageNumber - 1].ReleaseDate;
        }
        else
        {
            // Fixed stages - use predefined stage release date
            if (stageNumber < 1 || stageNumber > projectInfo.Stages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stageNumber),
                    $"Stage number {stageNumber} is out of range. Project has {projectInfo.Stages.Count} stages.");
            }

            stageReleaseDate = projectInfo.Stages[stageNumber - 1].ReleaseDate;
        }

        spendingTransaction.LockTime = Utils.DateTimeToUnixTime(stageReleaseDate.AddMinutes(1));

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation
        spendingTransaction.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var stageOutputs = investmentTransactionsHex
            .Select(trxHex => NBitcoin.Transaction.Parse(trxHex, nbitcoinNetwork))
            .Select(trx => AddInputToSpendingTransaction(projectInfo, stageNumber, trx, spendingTransaction))
            .ToList();

        var txSize = spendingTransaction.GetVirtualSize();
        var minimumFee = new FeeRate(Money.Satoshis(1100)).GetFee(txSize); //1000 sats per kilobyte

        var totalFee = nbitcoinNetwork
            .CreateTransactionBuilder()
            .AddCoins(stageOutputs.Select(_ => _.ToCoin()))
            .EstimateFees(spendingTransaction, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        spendingTransaction.Outputs[0].Value -= totalFee < minimumFee ? minimumFee : totalFee;

        _logger.LogInformation($"Unsigned spendingTransaction hex {spendingTransaction.ToHex()}");

        // Step 4 - sign the taproot inputs
        var trxData = spendingTransaction.PrecomputeTransactionData(stageOutputs.Select(_ => _.TxOut).ToArray());
        const TaprootSigHash sigHash = TaprootSigHash.All;
        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));

        var inputIndex = 0;
        foreach (var input in spendingTransaction.Inputs)
        {
            var scriptToExecute = new NBitcoin.Script(input.WitScript[1]);
            var controlBlock = input.WitScript[2];

            var tapScript = scriptToExecute.ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(inputIndex, tapScript.LeafHash) { SigHash = sigHash };
            var hash = spendingTransaction.GetSignatureHashTaproot(trxData, execData);

            _logger.LogInformation($"sig hash of inputIndex {inputIndex} spendingTransaction hex {hash.ToString()}");

            var sig = key.SignTaprootKeySpend(hash, sigHash);

            _logger.LogInformation($"sig of inputIndex {inputIndex} spendingTransaction hex {sig.ToString()}");

            // todo: throw a proper exception
            Debug.Assert(key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature));

            input.WitScript = new WitScript(
                Op.GetPushOp(sig.ToBytes()),
                Op.GetPushOp(scriptToExecute.ToBytes()),
                Op.GetPushOp(controlBlock));

            _logger.LogInformation($"WitScript of inputIndex {inputIndex} spendingTransaction hex {input.WitScript.ToString()}");

            inputIndex++;
        }

        _logger.LogInformation($"signed spendingTransaction hex {spendingTransaction.ToHex()}");

        var finalTrx = network.CreateTransaction(spendingTransaction.ToHex());

        return new TransactionInfo { Transaction = finalTrx, TransactionFee = totalFee };
    }

    public Transaction CreateNewProjectTransaction(string founderKey, Script angorKey, long angorFeeSatoshis, short keyType, string nostrEventId)
    {
        var projectStartTransaction = _networkConfiguration.GetNetwork()
            .Consensus.ConsensusFactory.CreateTransaction();

        // create the output and script of the project id
        var investorInfoOutput = new Blockcore.Consensus.TransactionInfo.TxOut(
            new Blockcore.NBitcoin.Money(angorFeeSatoshis), angorKey);

        projectStartTransaction.AddOutput(investorInfoOutput);

        // todo: here we should add the hash of the project data as opreturn

        // create the output and script of the investor pubkey script opreturn
        var angorFeeOutputScript = _projectScriptsBuilder.BuildFounderInfoScript(founderKey, keyType, nostrEventId);
        var founderOPReturnOutput = new Blockcore.Consensus.TransactionInfo.TxOut(
            new Blockcore.NBitcoin.Money(0), angorFeeOutputScript);
        projectStartTransaction.AddOutput(founderOPReturnOutput);

        return projectStartTransaction;
    }

    private IndexedTxOut AddInputToSpendingTransaction(ProjectInfo projectInfo, int stageNumber, NBitcoin.Transaction trx,
         NBitcoin.Transaction spendingTransaction)
    {
        var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1);

        spendingTransaction.Outputs[0].Value += stageOutput.TxOut.Value;

        var input = spendingTransaction.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null, new NBitcoin.Sequence(spendingTransaction.LockTime.Value));

        var (investorKey, secretHash) = GetProjectDetailsFromOpReturn(trx);

        // Extract dynamic stage info for Fund/Subscribe projects
        DateTime? investmentStartDate = null;
        byte patternIndex = 0;

        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            var opReturnOutput = trx.Outputs.AsIndexedOutputs().ElementAt(1);
            var dynamicStageInfo = _projectScriptsBuilder.GetDynamicStageInfoFromOpReturnScript(
               new Script(opReturnOutput.TxOut.ScriptPubKey.ToBytes()));

            if (dynamicStageInfo != null)
            {
                investmentStartDate = dynamicStageInfo.GetInvestmentStartDate();
                patternIndex = dynamicStageInfo.PatternId;
            }
        }

        DateTime? expiryDateOverride = PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, trx.GetTotalInvestmentAmount());

        ProjectScripts scriptStages;

        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            // Dynamic stages - pass investment start date, pattern index, and expiry override
            scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(
            projectInfo,
            investorKey,
            stageNumber - 1,
            secretHash,
            expiryDateOverride,
            investmentStartDate,
            patternIndex);
        }
        else
        {
            // Fixed stages - pass expiry override
            scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(
                projectInfo,
                investorKey,
                stageNumber - 1,
                secretHash,
                expiryDateOverride);
        }

        var controlBlock = _taprootScriptBuilder.CreateControlBlock(scriptStages, _ => _.Founder);

        // use fake data for fee estimation
        var sigPlaceHolder = new byte[65];

        input.WitScript = new WitScript(Op.GetPushOp(sigPlaceHolder), Op.GetPushOp(scriptStages.Founder.ToBytes()),
                 Op.GetPushOp(controlBlock.ToBytes()));

        return stageOutput;
    }

    private (string investorKey, uint256? secretHash) GetProjectDetailsFromOpReturn(NBitcoin.Transaction investmentTransaction)
    {
        var opretunOutput = investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(1);

        return
            _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(
                new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));
    }
}
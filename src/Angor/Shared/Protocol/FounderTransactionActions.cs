using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Utilities;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
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
        var network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTrxHex, nbitcoinNetwork);

        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));
        const TaprootSigHash sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, network.CreateTransaction(investmentTrxHex));

        var (investorKey, secretHash) = GetProjectDetailsFromOpReturn(nbitcoinInvestmentTransaction);

        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Where(txout => txout.TxOut.ScriptPubKey.IsScriptType(ScriptType.Taproot))
            .Select(_ => _.TxOut)
            .ToArray();

        SignatureInfo info = new SignatureInfo { ProjectIdentifier = projectInfo.ProjectIdentifier };

        for (var stageIndex = 0; stageIndex < outputs.Length; stageIndex++)
        {
            ProjectScripts scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageIndex);

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

    public TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, int stageNumber, Script founderRecieveAddress, string founderPrivateKey, FeeEstimation fee)
    {
        // For Invest projects, all transactions use the same stage number
        var stageIndexes = Enumerable.Repeat(stageNumber, investmentTransactionsHex.Count()).ToList();
        return SpendFounderStage(projectInfo, investmentTransactionsHex, stageIndexes, founderRecieveAddress, founderPrivateKey, fee);
    }

    public TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, IEnumerable<int> stageNumbers, Script founderRecieveAddress, string founderPrivateKey, FeeEstimation fee)
    {
        var network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var spendingTransaction = nbitcoinNetwork.CreateTransaction();

        var transactionList = investmentTransactionsHex.ToList();
        var stageNumbersList = stageNumbers.ToList();

        if (transactionList.Count != stageNumbersList.Count)
        {
            throw new ArgumentException($"Number of transactions ({transactionList.Count}) must match number of stage numbers ({stageNumbersList.Count})");
        }

        // Step 1 - the time lock
        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        DateTime stageReleaseDate;

        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            // Dynamic stages - find the latest expired stage release date from all investment transactions
            stageReleaseDate = FindLatestExpiredStageReleaseDate(projectInfo, transactionList, stageNumbersList);
        }
        else
        {
            // Fixed stages - use predefined stage release date (all transactions should have same stage number)
            var stageNumber = stageNumbersList.First();
            if (stageNumber < 1 || stageNumber > projectInfo.Stages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stageNumbers),
                      $"Stage number {stageNumber} is out of range. Project has {projectInfo.Stages.Count} stages.");
            }

            stageReleaseDate = projectInfo.Stages[stageNumber - 1].ReleaseDate;
        }

        spendingTransaction.LockTime = Utils.DateTimeToUnixTime(stageReleaseDate.AddMinutes(1));

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation
        spendingTransaction.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var stageOutputs = new List<IndexedTxOut>();
        for (int i = 0; i < transactionList.Count; i++)
        {
            var output = AddInputToSpendingTransaction(projectInfo, stageNumbersList[i], transactionList[i], spendingTransaction);
            stageOutputs.Add(output);
        }

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

    /// <summary>
    /// Finds the latest (most recent) expired stage release date from all investment transactions.
    /// This is needed for Fund/Subscribe projects where different investors may have different stage schedules.
    /// The founder can only spend a stage after ALL investors' stages have expired.
    /// </summary>
    private DateTime FindLatestExpiredStageReleaseDate(ProjectInfo projectInfo, List<string> investmentTransactionsHex, List<int> stageNumbers)
    {
        var network = _networkConfiguration.GetNetwork();
        var latestReleaseDate = DateTime.MinValue;

        for (int i = 0; i < investmentTransactionsHex.Count; i++)
        {
            var investmentTrxHex = investmentTransactionsHex[i];
            var stageNumber = stageNumbers[i];

            try
            {
                // Parse the investment transaction
                var investmentTrx = network.CreateTransaction(investmentTrxHex);

                // Create funding parameters from the transaction to get investment start date and pattern
                var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, network.CreateTransaction(investmentTrxHex));

                // Validate that we have the required data for dynamic stages
                if (!fundingParameters.InvestmentStartDate.HasValue)
                {
                    _logger.LogWarning($"Investment transaction {investmentTrx.GetHash()} does not have an InvestmentStartDate. Skipping.");
                    continue;
                }

                if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
                {
                    throw new InvalidOperationException("Fund/Subscribe projects must have at least one DynamicStagePattern");
                }

                if (fundingParameters.PatternIndex >= projectInfo.DynamicStagePatterns.Count)
                {
                    _logger.LogWarning($"Investment transaction {investmentTrx.GetHash()} has invalid PatternIndex {fundingParameters.PatternIndex}. Skipping.");
                    continue;
                }

                // Get the pattern for this investment
                var pattern = projectInfo.DynamicStagePatterns[fundingParameters.PatternIndex];

                // Calculate the release date for the specified stage (stageNumber is 1-based, so subtract 1)
                var stageIndex = stageNumber - 1;
                var stageReleaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(
                    fundingParameters.InvestmentStartDate.Value,
                    pattern,
                    stageIndex);

                // Track the latest (most recent) release date
                if (stageReleaseDate > latestReleaseDate)
                {
                    latestReleaseDate = stageReleaseDate;
                    _logger.LogInformation($"Found later stage release date: {stageReleaseDate:yyyy-MM-dd HH:mm:ss} from transaction {investmentTrx.GetHash()} stage {stageNumber}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing investment transaction for stage release date calculation. Skipping this transaction.");
                continue;
            }
        }

        if (latestReleaseDate == DateTime.MinValue)
        {
            throw new InvalidOperationException("Could not determine stage release date from any investment transaction. No valid transactions found.");
        }

        _logger.LogInformation($"Latest expired stage release date: {latestReleaseDate:yyyy-MM-dd HH:mm:ss}");

        return latestReleaseDate;
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

    private IndexedTxOut AddInputToSpendingTransaction(ProjectInfo projectInfo, int stageNumber, string investorTrxHash,
         NBitcoin.Transaction spendingTransaction)
    {
        var network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = NetworkMapper.Map(network);
        NBitcoin.Transaction trx = NBitcoin.Transaction.Parse(investorTrxHash, nbitcoinNetwork);

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, network.CreateTransaction(investorTrxHash));

        var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1);

        spendingTransaction.Outputs[0].Value += stageOutput.TxOut.Value;

        var input = spendingTransaction.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null, new NBitcoin.Sequence(spendingTransaction.LockTime.Value));

        //var (investorKey, secretHash) = GetProjectDetailsFromOpReturn(trx);

        //var effectiveExpiryDate = fundingParameters.ExpiryDateOverride ?? projectInfo.ExpiryDate;

        //DateTime? expiryDateOverride = PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, trx.GetTotalInvestmentAmount());

        ProjectScripts scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageNumber - 1);

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
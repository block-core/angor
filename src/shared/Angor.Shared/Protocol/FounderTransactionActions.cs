using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Buffers.Binary;

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
        Transaction recoveryTransaction, AngorKey founderPrivateKey)
    {
        var network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = network.BitcoinNetwork;
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTrxHex, nbitcoinNetwork);

        var key = founderPrivateKey;
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

            _logger.LogDebug("Creating recovery signature for project={ProjectId}, stage={Stage}", projectInfo.ProjectIdentifier, stageIndex);

            var result = new TaprootPubKey(
                Angor.Shared.Protocol.Scripts.TaprootKeyHelper.GetTaprootOutputKeyBytes(key.PubKey))
                .VerifySignature(hash, TaprootSignature.Parse(sig).SchnorrSignature);

            _logger.LogInformation($"verification = {result}");

            info.Signatures.Add(new SignatureInfoItem { Signature = sig, StageIndex = stageIndex });
        }

        return info;
    }

    public TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, int stageNumber, Script founderRecieveAddress, AngorKey founderPrivateKey, FeeEstimation fee)
    {
        // For Invest projects, all transactions use the same stage number
        var stageTransactionInputs = investmentTransactionsHex.Select(trx => new StageTransactionInput(trx, stageNumber)).ToList();
        return SpendFounderStage(projectInfo, stageTransactionInputs, founderRecieveAddress, founderPrivateKey, fee);
    }

    public TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<StageTransactionInput> stageTransactionInput, Script founderRecieveAddress, AngorKey founderPrivateKey, FeeEstimation fee)
    {
        // H4: Reject fee rates below the protocol minimum — a sub-minimum rate
        // indicates a bug in fee estimation or a unit mismatch. FeeRate must be in sat/kB.
        if (fee.FeeRate < ProtocolConstants.MinFeeRateSatsPerKb)
            throw new ArgumentOutOfRangeException(nameof(fee),
                $"Fee rate {fee.FeeRate} sat/kB is below the protocol minimum of {ProtocolConstants.MinFeeRateSatsPerKb} sat/kB.");

        var network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = network.BitcoinNetwork;
        var spendingTransaction = nbitcoinNetwork.CreateTransaction();

        // Step 1 - the time lock
        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        DateTime stageReleaseDate;

        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            // Dynamic stages - find the latest expired stage release date from all investment transactions
            stageReleaseDate = FindLatestExpiredStageReleaseDate(projectInfo, stageTransactionInput);
        }
        else
        {
            // Fixed stages - use predefined stage release date (all transactions should have same stage number)
            var stageNumber = stageTransactionInput.First().StageNumber;
            if (stageNumber < 1 || stageNumber > projectInfo.Stages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stageNumber), $"Stage number {stageNumber} is out of range. Project has {projectInfo.Stages.Count} stages.");
            }

            stageReleaseDate = projectInfo.Stages[stageNumber - 1].ReleaseDate;
        }

        spendingTransaction.LockTime = Utils.DateTimeToUnixTime(stageReleaseDate.AddMinutes(1));

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation
        spendingTransaction.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var stageOutputs = new List<IndexedTxOut>();
        foreach( var transactionList in stageTransactionInput)
        {
            var output = AddInputToSpendingTransaction(projectInfo, transactionList, spendingTransaction);
            stageOutputs.Add(output);
        }

        var txSize = spendingTransaction.GetVirtualSize();
        var minimumFee = new FeeRate(Money.Satoshis(ProtocolConstants.MinFeeRateSatsPerKb)).GetFee(txSize);

        var totalFee = nbitcoinNetwork
            .CreateTransactionBuilder()
            .AddCoins(stageOutputs.Select(_ => _.ToCoin()))
            .EstimateFees(spendingTransaction, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        var appliedFee = totalFee < minimumFee ? minimumFee : totalFee;

        if (spendingTransaction.Outputs[0].Value <= appliedFee)
        {
            throw new InvalidOperationException(
                $"Stage funds ({spendingTransaction.Outputs[0].Value.Satoshi} sats) are insufficient to cover the transaction fee ({appliedFee.Satoshi} sats). " +
                $"The stage amount must be greater than the fee.");
        }

        spendingTransaction.Outputs[0].Value -= appliedFee;

        _logger.LogDebug("Unsigned spending transaction prepared with {InputCount} inputs", spendingTransaction.Inputs.Count);

        // Step 4 - sign the taproot inputs
        var trxData = spendingTransaction.PrecomputeTransactionData(stageOutputs.Select(_ => _.TxOut).ToArray());
        const TaprootSigHash sigHash = TaprootSigHash.All;
        var key = founderPrivateKey;

        var inputIndex = 0;
        foreach (var input in spendingTransaction.Inputs)
        {
            var scriptToExecute = new NBitcoin.Script(input.WitScript[1]);
            var controlBlock = input.WitScript[2];

            var tapScript = scriptToExecute.ToTapScript(TapLeafVersion.C0);
            var execData = new TaprootExecutionData(inputIndex, tapScript.LeafHash) { SigHash = sigHash };
            var hash = spendingTransaction.GetSignatureHashTaproot(trxData, execData);

            var sig = key.SignTaprootKeySpend(hash, sigHash);

            if (!key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature))
                throw new InvalidOperationException($"Taproot signature verification failed for input {inputIndex}");

            input.WitScript = new WitScript(
                Op.GetPushOp(sig.ToBytes()),
                Op.GetPushOp(scriptToExecute.ToBytes()),
                Op.GetPushOp(controlBlock));

            _logger.LogDebug("Signed input {InputIndex}", inputIndex);

            inputIndex++;
        }

        _logger.LogDebug("Spending transaction signed successfully");

        var finalTrx = network.CreateTransaction(spendingTransaction.ToHex());

        return new TransactionInfo { Transaction = finalTrx, TransactionFee = totalFee };
    }

    /// <summary>
    /// Finds the latest (most recent) expired stage release date from all investment transactions.
    /// This is needed for Fund/Subscribe projects where different investors may have different stage schedules.
    /// The founder can only spend a stage after ALL investors' stages have expired.
    /// </summary>
    private DateTime FindLatestExpiredStageReleaseDate(ProjectInfo projectInfo, IEnumerable<StageTransactionInput> stageTransactionInput)
    {
        var network = _networkConfiguration.GetNetwork();
        var latestReleaseDate = DateTime.MinValue;

        foreach( StageTransactionInput input in stageTransactionInput ) 
        {
            var investmentTrxHex = input.TransactionHex;
            var stageNumber = input.StageNumber;

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

                var pattern = fundingParameters.FindPattern(projectInfo);

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
        var network = _networkConfiguration.GetNetwork();
        var projectStartTransaction = network.CreateTransaction();

        // create the output and script of the project id
        projectStartTransaction.Outputs.Add(new TxOut(new Money(angorFeeSatoshis), angorKey));

        // todo: here we should add the hash of the project data as opreturn

        // create the output and script of the investor pubkey script opreturn
        var angorFeeOutputScript = _projectScriptsBuilder.BuildFounderInfoScript(founderKey, keyType, nostrEventId);
        projectStartTransaction.Outputs.Add(new TxOut(Money.Zero, angorFeeOutputScript));

        return projectStartTransaction;
    }

    private IndexedTxOut AddInputToSpendingTransaction(ProjectInfo projectInfo, StageTransactionInput stageTransactionInput, Transaction spendingTransaction)
    {
        var network = _networkConfiguration.GetNetwork();
        var nbitcoinNetwork = network.BitcoinNetwork;
        Transaction trx = Transaction.Parse(stageTransactionInput.TransactionHex, nbitcoinNetwork);

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, network.CreateTransaction(stageTransactionInput.TransactionHex));

        var stageOutput = trx.Outputs.AsIndexedOutputs()
          .Where(txout => txout.TxOut.ScriptPubKey.IsScriptType(ScriptType.Taproot))
          .ElementAt(stageTransactionInput.StageNumber - 1);

        spendingTransaction.Outputs[0].Value += stageOutput.TxOut.Value;

        var input = spendingTransaction.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null, new NBitcoin.Sequence(0xFFFFFFFE));

        ProjectScripts scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, stageTransactionInput.StageNumber - 1);

        var controlBlock = _taprootScriptBuilder.CreateControlBlock(scriptStages, _ => _.Founder);

        // use fake data for fee estimation
        var sigPlaceHolder = new byte[65];

        input.WitScript = new WitScript(
            Op.GetPushOp(sigPlaceHolder), 
            Op.GetPushOp(scriptStages.Founder.ToBytes()),
            Op.GetPushOp(controlBlock.ToBytes()));

        return stageOutput;
    }

    private (string investorKey, uint256? secretHash) GetProjectDetailsFromOpReturn(Transaction investmentTransaction)
    {
        var opretunOutput = investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(1);

        return
            _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(opretunOutput.TxOut.ScriptPubKey);
    }
}
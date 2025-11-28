using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Shared.Protocol.TransactionBuilders;

public class SpendingTransactionBuilder : ISpendingTransactionBuilder
{
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;

    public SpendingTransactionBuilder(INetworkConfiguration networkConfiguration, IProjectScriptsBuilder projectScriptsBuilder, IInvestmentScriptBuilder investmentScriptBuilder)
    {
        _networkConfiguration = networkConfiguration;
        _projectScriptsBuilder = projectScriptsBuilder;
        _investmentScriptBuilder = investmentScriptBuilder;
    }

    public TransactionInfo BuildRecoverInvestorRemainingFundsInProject(string investmentTransactionHex, ProjectInfo projectInfo,
        int startStageNumber, string receiveAddress, string privateKey, FeeRate feeRate,
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript)
    {
        var network = _networkConfiguration.GetNetwork();

        // We'll use the NBitcoin lib because its a taproot spend
        var nbitcoinNetwork = NetworkMapper.Map(network);

        var spendingTrx = nbitcoinNetwork.CreateTransaction();

        var fundingParameters = FundingParameters.CreateFromTransaction(projectInfo, network.CreateTransaction(investmentTransactionHex));

        var investmentTrxOutputs = GetInvestorTransactionData(investmentTransactionHex, startStageNumber, projectInfo);

        // Determine the effective expiry date
        // If expiryDateOverride is provided, use it; otherwise, use the project's standard ExpiryDate
        var effectiveExpiryDate = fundingParameters.ExpiryDateOverride ?? projectInfo.ExpiryDate;

        // Step 1 - the time lock
        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        spendingTrx.LockTime = Utils.DateTimeToUnixTime(effectiveExpiryDate.AddMinutes(1));

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation
        spendingTrx.Outputs.Add(investmentTrxOutputs.Sum(_ => _.TxOut.Value), NBitcoin.BitcoinAddress.Create(receiveAddress, nbitcoinNetwork));
        

        //Need to add the script sig to calculate the fee correctly
        spendingTrx.Inputs.AddRange(investmentTrxOutputs.Select((_, i) =>
         {
             // stage index is zero based so when calculating stage scripts
             // we need to adjust the stage number accordingly and to the stage we
             // spend from, e.g. startStageNumber = 3 means we spend from stage index 2
             var currentStageIndex = i + startStageNumber - 1;

             ProjectScripts scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, fundingParameters, currentStageIndex);

             var witScript = new WitScript(buildWitScriptWithSigPlaceholder(scriptStages).Pushes);

             return new TxIn(new OutPoint(_.Transaction, _.N))
             {
                 Sequence = new Sequence(spendingTrx.LockTime.Value),
                 WitScript = witScript
             };
         }));

        // Step 3 - calculate the fee and add a single output for all  inputs
        var feeToReduce = nbitcoinNetwork.CreateTransactionBuilder()
            .AddCoins(investmentTrxOutputs.Select(_ => _.ToCoin()))
            .EstimateFees(spendingTrx, feeRate);

        var totalSize = spendingTrx.GetVirtualSize(); //Same transaction builder bug 
        var minimumFee = new FeeRate(Money.Satoshis(feeRate.FeePerK))
            .GetFee(totalSize);

        if (feeToReduce.Satoshi < minimumFee)
            feeToReduce = minimumFee;

        spendingTrx.Outputs.Single().Value -= feeToReduce;

        // Step 4 - sign the taproot inputs
        var trxData = spendingTrx.PrecomputeTransactionData(investmentTrxOutputs.Select(s => s.TxOut).ToArray());
        var key = new Key(Encoders.Hex.DecodeData(privateKey));

        const TaprootSigHash sigHash = TaprootSigHash.All;
        var inputIndex = 0;
        foreach (var input in spendingTrx.Inputs)
        {
            var scriptToExecute =
            new NBitcoin.Script(
           input.WitScript[input.WitScript.PushCount - 2]); //control block is the last and execute one before it

            var hash = spendingTrx.GetSignatureHashTaproot(trxData,
                new TaprootExecutionData(inputIndex, scriptToExecute.ToTapScript(TapLeafVersion.C0).LeafHash) { SigHash = sigHash });

            var sig = key.SignTaprootKeySpend(hash, sigHash);

            input.WitScript = new WitScript(addSignatureToWitScript(input.WitScript, sig).ToBytes());

            inputIndex++;
        }

        var transaction = network.CreateTransaction(spendingTrx.ToHex());

        return new TransactionInfo { Transaction = transaction, TransactionFee = feeToReduce };
    }

    private List<IndexedTxOut> GetInvestorTransactionData(string investorTrxHash, int spendingStartStageNumber, ProjectInfo projectInfo)
    {
        var network = _networkConfiguration.GetNetwork();

        // We'll use the NBitcoin lib because its a taproot spend
        var nbitcoinNetwork = NetworkMapper.Map(network);

        var trx = NBitcoin.Transaction.Parse(investorTrxHash, nbitcoinNetwork);

        var investmentTrxOutputs = trx.Outputs.AsIndexedOutputs()
            .Where(utxo => utxo.TxOut.ScriptPubKey.IsScriptType(ScriptType.Taproot))
            .Skip(spendingStartStageNumber - 1)
            .ToList();

        return investmentTrxOutputs;
    }
}
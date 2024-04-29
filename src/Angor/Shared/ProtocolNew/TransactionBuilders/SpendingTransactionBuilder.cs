using Angor.Shared.Models;
using Angor.Shared.ProtocolNew.Scripts;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Shared.ProtocolNew.TransactionBuilders;

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

    public TransactionInfo BuildRecoverInvestorRemainingFundsInProject(string investmentTransactionHex ,ProjectInfo projectInfo, 
        int startStageIndex, string receiveAddress, string privateKey, FeeRate feeRate, 
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript)
    {
        var network = _networkConfiguration.GetNetwork();
        
        // We'll use the NBitcoin lib because its a taproot spend
        var nbitcoinNetwork = NetworkMapper.Map(network);

        var spendingTrx = nbitcoinNetwork.CreateTransaction();

        var (investorKey, secretHash, investmentTrxOutputs) = GetInvestorTransactionData(investmentTransactionHex, startStageIndex);

        // Step 1 - the time lock
        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        spendingTrx.LockTime = Utils.DateTimeToUnixTime(projectInfo.ExpiryDate.AddMinutes(1));
        
        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation
        spendingTrx.Outputs.Add(investmentTrxOutputs.Sum(_ => _.TxOut.Value), NBitcoin.BitcoinAddress.Create(receiveAddress, nbitcoinNetwork));
        
        //Need to add the script sig to calculate the fee correctly
        spendingTrx.Inputs.AddRange(investmentTrxOutputs.Select((_,i) =>
        {
            var currentStageIndex = i + startStageIndex;
            
            var scriptStages =  _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, 
                    currentStageIndex, secretHash);

            var witScript =  new WitScript(buildWitScriptWithSigPlaceholder(scriptStages).Pushes);

            return new TxIn(new OutPoint(_.Transaction, _.N))
                { Sequence = new Sequence(spendingTrx.LockTime.Value),  WitScript = witScript};
        }));
        
        // Step 3 - calculate the fee and add a single output for all  inputs
        var feeToReduce = nbitcoinNetwork.CreateTransactionBuilder()
            .AddCoins(investmentTrxOutputs.Select(_ => _.ToCoin()))
            .EstimateFees(spendingTrx, feeRate);

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
                new TaprootExecutionData(inputIndex, scriptToExecute.TaprootV1LeafHash) { SigHash = sigHash });

            var sig = key.SignTaprootKeySpend(hash, sigHash);

            input.WitScript = new WitScript(addSignatureToWitScript(input.WitScript ,sig).ToBytes());

            inputIndex++;
        }

        var transaction = network.CreateTransaction(spendingTrx.ToHex());

        return new TransactionInfo { Transaction = transaction, TransactionFee = feeToReduce };
    }

    private (string investorKey, uint256? secretHash, List<IndexedTxOut> investmentTrxOutputs) GetInvestorTransactionData(
        string investorTrxHash, int spendingStartStage)
    {
        var network = _networkConfiguration.GetNetwork();
        
        // We'll use the NBitcoin lib because its a taproot spend
        var nbitcoinNetwork = NetworkMapper.Map(network);
        
        var trx = NBitcoin.Transaction.Parse(investorTrxHash, nbitcoinNetwork);
        var opretunOutput = trx.Outputs.AsIndexedOutputs().ElementAt(1);
        
        var (investorKey, secretHash) = _projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(
            new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

        var investmentTrxOutputs = trx.Outputs.AsIndexedOutputs()
            .Where((_, i) => i >= spendingStartStage + 2 && //ignore the first 2 outputs as they are not for the stages
                             _.TxOut.ScriptPubKey.IsScriptType(ScriptType.Taproot)) //probably this condition is not needed
            .ToList();

        return (investorKey, secretHash, investmentTrxOutputs);
    }
}
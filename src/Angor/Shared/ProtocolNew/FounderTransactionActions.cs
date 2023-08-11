using System.Diagnostics;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using IndexedTxOut = NBitcoin.IndexedTxOut;
using Key = NBitcoin.Key;
using Op = NBitcoin.Op;
using OutPoint = NBitcoin.OutPoint;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using Utils = NBitcoin.Utils;
using WitScript = NBitcoin.WitScript;

namespace Angor.Shared.ProtocolNew;

public class FounderTransactionActions : IFounderTransactionActions
{
    private readonly INetworkConfiguration _networkConfiguration;

    public FounderTransactionActions(INetworkConfiguration networkConfiguration)
    {
        _networkConfiguration = networkConfiguration;
    }

    public List<string> FounderSignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex, 
        IEnumerable<Transaction> transactions, string founderPrivateKey)
    {
        var network = _networkConfiguration.GetNetwork();
        
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var investmentTransaction = NBitcoin.Transaction.Parse(investmentTrxHex, nbitcoinNetwork);
        
        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));

        var opretunOutput = investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(1);

        var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

        var signatures = new List<string>();
        var index = 0;
        foreach (var transactionHex in transactions.Select(_ => _.ToHex()))
        {
            var stageTransaction = NBitcoin.Transaction.Parse(transactionHex, nbitcoinNetwork);
            
            var scriptStages = ScriptBuilder.BuildScripts(projectInfo.FounderKey,
                Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
                pubKeys.secretHash?.ToString(),
                projectInfo.Stages[index].ReleaseDate,
                projectInfo.ExpiryDate,
                projectInfo.ProjectSeeders);

            const TaprootSigHash sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;
            
            var hash = stageTransaction.GetSignatureHashTaproot(new[] { investmentTransaction.Outputs[index+2] },
                new TaprootExecutionData(0, 
                        new NBitcoin.Script(scriptStages.Recover.ToBytes()).TaprootV1LeafHash)
                    { SigHash = sigHash });
            
            signatures.Add(key.SignTaprootKeySpend(hash, sigHash).ToString());
            
            index++;
        }

        return signatures;
    }
    
     /// <summary>
    /// Allow the founder to spend the coins in a stage after the timelock has passed
    /// </summary>
    /// <exception cref="Exception"></exception>
    public Transaction SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, int stageNumber, Script founderRecieveAddress, string founderPrivateKey,
        FeeEstimation fee)
    {
        var network = _networkConfiguration.GetNetwork();
        
        // We'll use the NBitcoin lib because its a taproot spend
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        var spendingTransaction = nbitcoinNetwork.CreateTransaction();
        
        // Step 1 - the time lock
        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        spendingTransaction.LockTime = Utils.DateTimeToUnixTime(projectInfo.Stages[stageNumber - 1].ReleaseDate.AddMinutes(1));

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation
        spendingTransaction.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var stageOutputs = new List<IndexedTxOut>();
        
        foreach (var trxHex in investmentTransactionsHex)
        {
            var trx = NBitcoin.Transaction.Parse(trxHex, nbitcoinNetwork);
            var outputStage = AddInputToSpendingTransaction(projectInfo, stageNumber, trx, spendingTransaction);
            stageOutputs.Add(outputStage);
            builder.AddCoin( outputStage.ToCoin());
        }
        
        spendingTransaction.Outputs[0].Value -= builder
            .EstimateFees(spendingTransaction, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        // Step 4 - sign the taproot inputs
        var trxData = spendingTransaction.PrecomputeTransactionData(stageOutputs.Select(_ => _.TxOut).ToArray());

        var inputIndex = 0;
        foreach (var input in spendingTransaction.Inputs)
        {
            var scriptToExecute = new NBitcoin.Script(input.WitScript[1]);
            var controlBlock = input.WitScript[2];

            var sigHash = TaprootSigHash.All;// TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

            var execData = new TaprootExecutionData(inputIndex, scriptToExecute.TaprootV1LeafHash) { SigHash = sigHash };
            var hash = spendingTransaction.GetSignatureHashTaproot(trxData, execData);

            var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));
            var sig = key.SignTaprootKeySpend(hash, sigHash);

            Debug.Assert(key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature));
            
            input.WitScript = new WitScript(
                Op.GetPushOp(sig.ToBytes()),
                Op.GetPushOp(scriptToExecute.ToBytes()),
                Op.GetPushOp(controlBlock));

            inputIndex++;
        }

        return network.CreateTransaction(spendingTransaction.ToHex());
    }

     private static IndexedTxOut AddInputToSpendingTransaction(ProjectInfo projectInfo, int stageNumber, NBitcoin.Transaction trx,
         NBitcoin.Transaction spendingTransaction)
     {
         var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1);

         spendingTransaction.Outputs[0].Value += stageOutput.TxOut.Value;

         var input = spendingTransaction.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), 
             null, null, new NBitcoin.Sequence(spendingTransaction.LockTime.Value));

         var opReturnOutput = trx.Outputs.AsIndexedOutputs().ElementAt(1);

         var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opReturnOutput.TxOut.ScriptPubKey.ToBytes()));

         var scriptStages = ScriptBuilder.BuildScripts(projectInfo.FounderKey,
             Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
             pubKeys.secretHash?.ToString(),
             projectInfo.Stages[stageNumber - 1].ReleaseDate,
             projectInfo.ExpiryDate,
             projectInfo.ProjectSeeders);
         
         var controlBlock = AngorScripts.CreateControlBlockFounder(scriptStages);

         // use fake data for fee estimation
         var sigPlaceHolder = new byte[64];

         input.WitScript = new WitScript(Op.GetPushOp(sigPlaceHolder), Op.GetPushOp(scriptStages.Founder.ToBytes()),
             Op.GetPushOp(controlBlock.ToBytes()));
         
         return stageOutput;
     }
}
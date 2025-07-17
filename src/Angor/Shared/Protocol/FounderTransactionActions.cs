using System.Diagnostics;
using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using NBitcoin;
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

    public NBitcoin.PSBT CreateInvestorRecoveryPsbt(ProjectInfo projectInfo, string investmentTrxHex, 
        Transaction recoveryTransaction,  string rootExtPubKey, string path)
    {

        var nbitcoinNetwork = NetworkMapper.Map(_networkConfiguration.GetNetwork());
        var nbitcoinRecoveryTransaction = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);
        var nbitcoinInvestmentTransaction = NBitcoin.Transaction.Parse(investmentTrxHex, nbitcoinNetwork);

        NBitcoin.ExtPubKey accountExtPubKey = NBitcoin.ExtPubKey.Parse(rootExtPubKey, nbitcoinNetwork);
        NBitcoin.KeyPath keyPath = new KeyPath(path);
        NBitcoin.ExtPubKey accountExtPubKeyDerived = accountExtPubKey.Derive(keyPath);
        NBitcoin.RootedKeyPath rootedKeyPath = new NBitcoin.RootedKeyPath(accountExtPubKey, keyPath);

        var (investorKey, secretHash) = GetProjectDetailsFromOpReturn(nbitcoinInvestmentTransaction);
        
        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Skip(2).Take(projectInfo.Stages.Count)
            .Select(_ => _.TxOut)
            .ToArray();

        var psbt = NBitcoin.PSBT.FromTransaction(nbitcoinRecoveryTransaction, nbitcoinNetwork);

        for (var stageIndex = 0; stageIndex < projectInfo.Stages.Count; stageIndex++)
        {
            var scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, secretHash);
            var tapScript = new NBitcoin.Script(scriptStages.Recover.ToBytes()).ToTapScript(TapLeafVersion.C0);

            psbt.Inputs[stageIndex].TaprootSighashType = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;
            psbt.Inputs[stageIndex].WitnessUtxo = outputs[stageIndex];

            psbt.Inputs[stageIndex].HDTaprootKeyPaths.Add(
                accountExtPubKeyDerived.GetPublicKey().GetTaprootFullPubKey().InternalKey.AsTaprootPubKey(),
                new TaprootKeyPath(rootedKeyPath, new[] { tapScript.LeafHash }));

            // todo: 
            // https://github.com/bitcoin/bips/blob/master/bip-0174.mediawiki
            // https://github.com/bitcoin/bips/blob/master/bip-0371.mediawiki
            // https://github.com/bitcoin/bips/blob/master/bip-0370.mediawiki
            // the steps to complete this in nbitcoin
            // 1. add PSBT_IN_TAP_SCRIPT_SIG and PSBT_IN_TAP_LEAF_SCRIPT to the psbt (only key spend is supported we need to add support for script spend)
            // 2. we need to call the method SignTaprootKeySpend and pass the  LeafHash in the TaprootExecutionData for thet
            //    we need to pass the TapScript of the branch that is being executed, the spec says to put it in the PSBT_IN_TAP_LEAF_SCRIPT field
            // 3. apotential place to ptu itin NBitcoin is TaprootReadyPrecomputedTransactionData before we call the sign method
            // 4. then when we sign further down the stack we can check if there is a leaf then it is a HashVersion.Tapscript in the TaprootExecutionData
            // 5. this will automatically sign as a tapscript and not tapkey spend
            // 6. then store the sig in the TaprootKeySignature (however we need two such sigs so perhaps we should put the sigs in the PartialSigs field
           
        }

        return psbt;
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
        
        var outputs = nbitcoinInvestmentTransaction.Outputs.AsIndexedOutputs()
            .Skip(2).Take(projectInfo.Stages.Count)
            .Select(_ => _.TxOut)
            .ToArray();

        SignatureInfo info = new SignatureInfo { ProjectIdentifier = projectInfo.ProjectIdentifier };

        for (var stageIndex = 0; stageIndex < projectInfo.Stages.Count; stageIndex++)
        {
            var scriptStages = _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, secretHash);

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
        spendingTransaction.LockTime = Utils.DateTimeToUnixTime(projectInfo.Stages[stageNumber - 1].ReleaseDate.AddMinutes(1));

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

        return new TransactionInfo {Transaction = finalTrx, TransactionFee = totalFee};
    }

    public Transaction CreateNewProjectTransaction(string founderKey, Script angorKey, long angorFeeSatoshis, short keyType,  string nostrEventId)
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

         var input = spendingTransaction.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), 
             null, null, new NBitcoin.Sequence(spendingTransaction.LockTime.Value));

         var (investorKey, secretHash) = GetProjectDetailsFromOpReturn(trx);

         var scriptStages =  _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, 
             stageNumber - 1, secretHash);

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
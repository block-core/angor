﻿using System.Text;
using Angor.Shared;
using Angor.Shared.Models;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using NBitcoin.Policy;
using BitcoinAddress = Blockcore.NBitcoin.BitcoinAddress;
using FeeRate = Blockcore.NBitcoin.FeeRate;
using IndexedTxOut = NBitcoin.IndexedTxOut;
using Key = NBitcoin.Key;
using Money = Blockcore.NBitcoin.Money;
using Network = Blockcore.Networks.Network;
using Op = NBitcoin.Op;
using OutPoint = NBitcoin.OutPoint;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using ScriptType = NBitcoin.ScriptType;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using TransactionBuilder = Blockcore.Consensus.TransactionInfo.TransactionBuilder;
using TxOut = Blockcore.Consensus.TransactionInfo.TxOut;
using Utils = NBitcoin.Utils;
using WitScript = NBitcoin.WitScript;

namespace Angor.Test.DataBuilders;

public class InvestmentOperations
{
    private readonly IWalletOperations _walletOperations;

    public InvestmentOperations(IWalletOperations walletOperations, IDerivationOperations derivationOperations)
    {
        _walletOperations = walletOperations;
    }

    /// <summary>
    /// This method will create a transaction with all the spending conditions
    /// based on the project investment metadata the transaction will be unsigned (it wont have any inputs yet)
    /// </summary>
    public Transaction CreateInvestmentTransaction(Network network,InvestorContext context, long totalInvestmentAmount)
    {
        Transaction investmentTransaction = network.Consensus.ConsensusFactory.CreateTransaction();

        // create the output and script of the project id 
        var angorFeeOutputScript = ScriptBuilder.GetAngorFeeOutputScript(context.ProjectInfo.ProjectIdentifier);
        var angorOutput = new TxOut(new Money(totalInvestmentAmount / 100), angorFeeOutputScript);
        investmentTransaction.AddOutput(angorOutput);

        // create the output and script of the investor pubkey script opreturn

        var opreturnScript = ScriptBuilder.GetSeederInfoScript(context.InvestorKey, context.InvestorSecretHash);
        var investorInfoOutput = new TxOut(new Money(0), opreturnScript);
        investmentTransaction.AddOutput(investorInfoOutput);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = context.ProjectInfo.Stages
            .Select(_ => ScriptBuilder.BuildScripts(context.ProjectInfo.FounderKey, 
                context.ProjectInfo.FounderRecoveryKey,
                context.InvestorKey,
                context.InvestorSecretHash, 
                _.ReleaseDate, 
                context.ProjectInfo.ExpiryDate, 
                context.ProjectInfo.ProjectSeeders));

        var stagesScripts = stagesScript.Select(scripts =>
            AngorScripts.CreateStage(network, scripts));

        var stagesOutputs = stagesScripts.Select((_, i) =>
            new TxOut(new Money(GetPercentageOfAmountForStage(totalInvestmentAmount, context.ProjectInfo.Stages[i])),
                new Script(_.ToBytes())));

        foreach (var stagesOutput in stagesOutputs)
        {
            investmentTransaction.AddOutput(stagesOutput);
        }

        return investmentTransaction;
    }

    private static long GetPercentageOfAmountForStage(long amount, Stage stage)
    {
        return Convert.ToInt64(amount * stage.AmountToRelease);
    }
    
    public Transaction SignInvestmentTransaction(Network network,string changeAddress, Transaction transaction, WalletWords walletWords, List<UtxoDataWithPath> utxoDataWithPaths,
        FeeEstimation feeRate)
    {
        // We must use the NBitcoin lib because taproot outputs are non standard before taproot activated

        //var nbitcoinNetwork = NetworkMapper.Map(network);
        //var trx = NBitcoin.Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);

        var coins = _walletOperations.GetUnspentOutputsForTransaction(walletWords, utxoDataWithPaths);

        // var fees = _walletOperations.GetFeeEstimationAsync().GetAwaiter().GetResult();
        // var fee = fees.First(f => f.Confirmations == 1);


        //var incoins = coins.coins.Select(c => new NBitcoin.Coin(OutPoint.Parse(c.Outpoint.ToString()), new NBitcoin.TxOut(NBitcoin.Money.Satoshis(c.Amount.Satoshi), new NBitcoin.Script(c.ScriptPubKey.ToBytes()))));
        //var inKeys = coins.keys.Select(k => new Key(k.ToBytes())).ToArray();

        var builder = new TransactionBuilder(network) // nbitcoinNetwork.CreateTransactionBuilder()
            .AddCoins(coins.coins)
            .AddKeys(coins.keys.ToArray())
            .SetChange(BitcoinAddress.Create(changeAddress, network))
            .ContinueToBuild(transaction)
            .SendEstimatedFees(new FeeRate(Money.Satoshis(feeRate.FeeRate)))
            .CoverTheRest();

        var signTransaction = builder.BuildTransaction(true);// builder.SignTransactionInPlace(transaction);

        var verifyresult = builder.Verify(signTransaction, out Blockcore.NBitcoin.Policy.TransactionPolicyError[] result);

        if (!verifyresult)
        {
            StringBuilder sb = new();
            foreach (var policyError in result)
            {
                sb.AppendLine(policyError.ToString());
            }

            throw new Exception(sb.ToString());
        }

        return signTransaction;
    }

    /// <summary>
    /// Allow the founder to spend the coins in a stage after the timelock has passed
    /// </summary>
    /// <exception cref="Exception"></exception>
    public Transaction SpendFounderStage(Network network, FounderContext context, int stageNumber, Script founderRecieveAddress, string founderPrivateKey,
        FeeEstimation fee)
    {
        // We'll use the NBitcoin lib because its a taproot spend

        // var fees = _walletOperations.GetFeeEstimationAsync().Result;
        // var fee = fees.First(f => f.Confirmations == 1);

        var nbitcoinNetwork = NetworkMapper.Map(network);

        var spender = nbitcoinNetwork.CreateTransaction();
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        // Step 1 - the time lock

        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        var locktime = Utils.DateTimeToUnixTime(context.ProjectInfo.Stages[stageNumber - 1].ReleaseDate.AddMinutes(1));
        spender.LockTime = locktime;

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation

        spender.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var signingContext = new List<(NBitcoin.TxIn output, IndexedTxOut spendingOutput)>();

        foreach (var trxHex in context.InvestmentTrasnactionsHex)
        {
            var trx = NBitcoin.Transaction.Parse(trxHex, nbitcoinNetwork);

            var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1);

            spender.Outputs[0].Value += stageOutput.TxOut.Value;

            var input = spender.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null, new NBitcoin.Sequence(locktime));

            var opretunOutput = trx.Outputs.AsIndexedOutputs().ElementAt(1);

            var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

            var scriptStages = ScriptBuilder.BuildScripts(context.ProjectInfo.FounderKey,
                context.ProjectInfo.FounderRecoveryKey,
                Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
                pubKeys.secretHash?.ToString(),
                context.ProjectInfo.Stages[stageNumber - 1].ReleaseDate,
                context.ProjectInfo.ExpiryDate,
                context.ProjectSeeders);

            var controlBlock = AngorScripts.CreateControlBlock(scriptStages, _ => _.Founder);

            // use fake data for fee estimation
            var fakeSig = new byte[64];

            input.WitScript = new WitScript(Op.GetPushOp(fakeSig), Op.GetPushOp(scriptStages.Founder.ToBytes()), Op.GetPushOp(controlBlock.ToBytes()));

            signingContext.Add((input, stageOutput));
            builder.AddCoin(new NBitcoin.Coin(stageOutput));
        }

        // Step 3 - calculate the fee
       
        var feeToReduce = builder.EstimateFees(spender, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        spender.Outputs[0].Value -= feeToReduce;

        // Step 4 - sign the taproot inputs

        var inputIndex = 0;
        foreach (var item in signingContext)
        {
            var scriptToExecute = new NBitcoin.Script(item.output.WitScript[1]);
            var controlBlock = item.output.WitScript[2];

            var sighash = TaprootSigHash.All;// TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

            var allSpendingOutputs = signingContext.Select(s => s.spendingOutput.TxOut).ToArray();
            var trxData = spender.PrecomputeTransactionData(allSpendingOutputs); 
            var execData = new TaprootExecutionData(inputIndex, scriptToExecute.TaprootV1LeafHash) { SigHash = sighash };
            var hash = spender.GetSignatureHashTaproot(trxData, execData);

            var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));
            var sig = key.SignTaprootKeySpend(hash, sighash);

            if (!key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature))
            {
                throw new Exception();
            }

            item.output.WitScript = new WitScript(Op.GetPushOp(sig.ToBytes()), Op.GetPushOp(scriptToExecute.ToBytes()), Op.GetPushOp(controlBlock));
            inputIndex++;
        }

        if (!builder.Verify(spender, out TransactionPolicyError[] errors))
        {
            var sb = new StringBuilder();
            foreach (var transactionPolicyError in errors)
            {
                sb.AppendLine(transactionPolicyError.ToString());
            }

            throw new Exception(sb.ToString());
        }

        return network.CreateTransaction(spender.ToHex());
    }

    public List<Transaction> BuildRecoverInvestorFundsTransactions(InvestorContext context, Network network, string investorReceiveAddress)
    {
        // allow an investor that acquired enough seeder secrets to recover their investment
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var investmentTransaction = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);

        var founderStageSignatures = investmentTransaction.Outputs.AsIndexedOutputs()
            .Where(_ => _.TxOut.ScriptPubKey.IsScriptType(ScriptType.Taproot))
            .Select((_, i) =>
            {
                var stageTransaction = nbitcoinNetwork.CreateTransaction();

                var spendingScript = ScriptBuilder.GetInvestorPenaltyTransactionScript(
                    investorReceiveAddress,
                    context.ProjectInfo.PenaltyDays);
                
                stageTransaction.Outputs.Add(new NBitcoin.TxOut(_.TxOut.Value,
                    new NBitcoin.Script(spendingScript.WitHash.ScriptPubKey.ToBytes())));

                stageTransaction.Inputs.Add(new OutPoint(_.Transaction, _.N));

                return network.Consensus.ConsensusFactory.CreateTransaction(stageTransaction.ToHex());;
            });

        return founderStageSignatures.ToList();
    }

    public List<string> FounderSignInvestorRecoveryTransactions(InvestorContext context, Network network, List<Transaction> transactions, string founderPrivateKey)
    {
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var investmentTransaction = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);
        
        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));

        var opretunOutput = investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(1);

        var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

        return transactions.Select((_,i) => 
        {
            var stageTransaction = NBitcoin.Transaction.Parse(_.ToHex(), nbitcoinNetwork);
            
            var scriptStages = ScriptBuilder.BuildScripts(context.ProjectInfo.FounderKey,
                context.ProjectInfo.FounderRecoveryKey,
                Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
                pubKeys.secretHash?.ToString(),
                context.ProjectInfo.Stages[i].ReleaseDate,
                context.ProjectInfo.ExpiryDate,
                context.ProjectInfo.ProjectSeeders);

            const TaprootSigHash sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;
            
            var hash = stageTransaction.GetSignatureHashTaproot(new[] { investmentTransaction.Outputs[i+2] },
                new TaprootExecutionData(0, 
                        new NBitcoin.Script(scriptStages.Recover.ToBytes()).TaprootV1LeafHash)
                    { SigHash = sigHash });

            
            var signature = key.SignTaprootKeySpend(hash, sigHash);
            
            return signature.ToString();

        }).ToList();
    }
    
    public void AddWitScriptToInvestorRecoveryTransactions(InvestorContext context, Network network, List<Transaction> transactions, List<string> founderSignatures, string investorPrivateKey, string? seederSecret)
    {
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var investmentTransaction = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);

        var index = 0;
        var key = new Key(Encoders.Hex.DecodeData(investorPrivateKey));
        var secret = seederSecret != null ? new Key(Encoders.Hex.DecodeData(seederSecret)) : null;

        var opretunOutput = investmentTransaction.Outputs.AsIndexedOutputs().ElementAt(1);

        var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

        foreach (var transaction in transactions)
        {
            var stageTransaction = NBitcoin.Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);
            
            var projectScripts = ScriptBuilder.BuildScripts(context.ProjectInfo.FounderKey,
                context.ProjectInfo.FounderRecoveryKey,
                Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
                pubKeys.secretHash?.ToString(),
                context.ProjectInfo.Stages[index].ReleaseDate,
                context.ProjectInfo.ExpiryDate,
                context.ProjectInfo.ProjectSeeders);
            
            var controlBlock = AngorScripts.CreateControlBlock(projectScripts, _ => _.Recover);
            
            var sigHash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

            var hash = stageTransaction.GetSignatureHashTaproot(new[] { investmentTransaction.Outputs[index + 2] },
                new TaprootExecutionData(0,
                        new NBitcoin.Script(projectScripts.Recover.ToBytes()).TaprootV1LeafHash)
                    { SigHash = sigHash });
            
            var investorSignature = key.SignTaprootKeySpend(hash, sigHash);

            if (string.IsNullOrEmpty(context.InvestorSecretHash))
            {
                transaction.Inputs.Single().WitScript =
                    new Blockcore.Consensus.TransactionInfo.WitScript(
                        new WitScript(
                                Op.GetPushOp(investorSignature.ToBytes()),
                                Op.GetPushOp(TaprootSignature.Parse(founderSignatures[index]).ToBytes()),

                                Op.GetPushOp(projectScripts.Recover.ToBytes()),
                                Op.GetPushOp(controlBlock.ToBytes()))
                            .ToBytes());
            }
            else
            {
                if (secret == null)
                    throw new Exception("secret is missing");

                transaction.Inputs.Single().WitScript =
                    new Blockcore.Consensus.TransactionInfo.WitScript(
                        new WitScript(
                                Op.GetPushOp(secret.ToBytes()),
                                Op.GetPushOp(investorSignature.ToBytes()),
                                Op.GetPushOp(TaprootSignature.Parse(founderSignatures[index]).ToBytes()),

                                Op.GetPushOp(projectScripts.Recover.ToBytes()),
                                Op.GetPushOp(controlBlock.ToBytes()))
                            .ToBytes());
            }

            index++;
        }
    }

    /// <summary>
    /// allow an investor that take back any coins left when the project end date has passed
    /// </summary>
    public Transaction RecoverEndOfProjectFunds(Network network, InvestorContext context, int[] stages, Script investorRecieveAddress, string investorPrivateKey)
    {
        // We'll use the NBitcoin lib because its a taproot spend

        var fees = _walletOperations.GetFeeEstimationAsync().Result;
        var fee = fees.First(f => f.Confirmations == 1);

        var nbitcoinNetwork = NetworkMapper.Map(network);

        var spender = nbitcoinNetwork.CreateTransaction();
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        // Step 1 - the time lock

        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        var locktime = Utils.DateTimeToUnixTime(context.ProjectInfo.ExpiryDate.AddMinutes(1));
        spender.LockTime = locktime;

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation

        spender.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(investorRecieveAddress.ToBytes()));

        var signingContext = new List<(NBitcoin.TxIn output, IndexedTxOut spendingOutput)>();

        var trx = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);

        foreach (var stageNumber in stages)
        {
            var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1);

            spender.Outputs[0].Value += stageOutput.TxOut.Value;

            var input = spender.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null, new NBitcoin.Sequence(locktime));

            var opretunOutput = trx.Outputs.AsIndexedOutputs().ElementAt(1);

            var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

            var scriptStages = ScriptBuilder.BuildScripts(context.ProjectInfo.FounderKey,
                context.ProjectInfo.FounderRecoveryKey,
                Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
                pubKeys.secretHash?.ToString(),
                context.ProjectInfo.Stages[stageNumber - 1].ReleaseDate,
                context.ProjectInfo.ExpiryDate,
                context.ProjectInfo.ProjectSeeders);

            var controlBlock = AngorScripts.CreateControlBlock(scriptStages, _ => _.EndOfProject);

            // use fake data for fee estimation
            var fakeSig = new byte[64];

            input.WitScript = new WitScript(Op.GetPushOp(fakeSig), Op.GetPushOp(scriptStages.EndOfProject.ToBytes()), Op.GetPushOp(controlBlock.ToBytes()));

            signingContext.Add((input, stageOutput));
            builder.AddCoin(new NBitcoin.Coin(stageOutput));
        }

        // Step 3 - calculate the fee

        var feeToReduce = builder.EstimateFees(spender, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        spender.Outputs[0].Value -= feeToReduce;

        // Step 4 - sign the taproot inputs

        var inputIndex = 0;
        foreach (var item in signingContext)
        {
            var scriptToExecute = new NBitcoin.Script(item.output.WitScript[1]);
            var controlBlock = item.output.WitScript[2];

            var sighash = TaprootSigHash.All;// TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

            var allSpendingOutputs = signingContext.Select(s => s.spendingOutput.TxOut).ToArray();
            var trxData = spender.PrecomputeTransactionData(allSpendingOutputs);
            var execData = new TaprootExecutionData(inputIndex, scriptToExecute.TaprootV1LeafHash) { SigHash = sighash };
            var hash = spender.GetSignatureHashTaproot(trxData, execData);

            var key = new Key(Encoders.Hex.DecodeData(investorPrivateKey));
            var sig = key.SignTaprootKeySpend(hash, sighash);

            if (!key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature))
            {
                throw new Exception();
            }

            item.output.WitScript = new WitScript(Op.GetPushOp(sig.ToBytes()), Op.GetPushOp(scriptToExecute.ToBytes()), Op.GetPushOp(controlBlock));
            inputIndex++;
        }

        if (!builder.Verify(spender, out TransactionPolicyError[] errors))
        {
            var sb = new StringBuilder();
            foreach (var transactionPolicyError in errors)
            {
                sb.AppendLine(transactionPolicyError.ToString());
            }

            throw new Exception(sb.ToString());
        }

        return network.CreateTransaction(spender.ToHex());
    }

    /// <summary>
    /// allow an investor that take back any coins left when the project end date has passed
    /// </summary>
    public Transaction RecoverFundsNoPenalty(Network network, InvestorContext context, int[] stages, Blockcore.NBitcoin.Key[] seederSecrets, Script investorRecieveAddress, string investorPrivateKey)
    {
        // We'll use the NBitcoin lib because its a taproot spend

        var fees = _walletOperations.GetFeeEstimationAsync().Result;
        var fee = fees.First(f => f.Confirmations == 1);

        var nbitcoinNetwork = NetworkMapper.Map(network);

        var spender = nbitcoinNetwork.CreateTransaction();
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        // Step 1 - the time lock

        // we must set the locktime to be ahead of the current block time
        // and ahead of the cltv otherwise the trx wont get accepted in the chain
        var locktime = Utils.DateTimeToUnixTime(context.ProjectInfo.ExpiryDate.AddMinutes(1));
        spender.LockTime = locktime;

        // Step 2 - build the transaction outputs and inputs without signing using fake sigs for fee estimation

        spender.Outputs.Add(NBitcoin.Money.Zero, new NBitcoin.Script(investorRecieveAddress.ToBytes()));

        var signingContext = new List<(NBitcoin.TxIn output, IndexedTxOut spendingOutput)>();

        var trx = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);

        foreach (var stageNumber in stages)
        {
            var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 1);

            spender.Outputs[0].Value += stageOutput.TxOut.Value;

            var input = spender.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null, new NBitcoin.Sequence(locktime));

            var opretunOutput = trx.Outputs.AsIndexedOutputs().ElementAt(1);

            var pubKeys = ScriptBuilder.GetInvestmentDataFromOpReturnScript(new Script(opretunOutput.TxOut.ScriptPubKey.ToBytes()));

            var scriptStages = ScriptBuilder.BuildScripts(context.ProjectInfo.FounderKey,
                context.ProjectInfo.FounderRecoveryKey,
                Encoders.Hex.EncodeData(pubKeys.investorKey.ToBytes()),
                pubKeys.secretHash?.ToString(),
                context.ProjectInfo.Stages[stageNumber - 1].ReleaseDate,
                context.ProjectInfo.ExpiryDate,
                context.ProjectInfo.ProjectSeeders);

            var result = AngorScripts.CreateControlSeederSecrets(scriptStages, seederSecrets);

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

            input.WitScript = new WitScript(ops.ToArray());

            signingContext.Add((input, stageOutput));
            builder.AddCoin(new NBitcoin.Coin(stageOutput));
        }

        // Step 3 - calculate the fee

        var feeToReduce = builder.EstimateFees(spender, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        spender.Outputs[0].Value -= feeToReduce;

        // Step 4 - sign the taproot inputs

        var inputIndex = 0;
        foreach (var item in signingContext)
        {
            var controBlock = new NBitcoin.Script(item.output.WitScript[item.output.WitScript.PushCount - 1]);
            var scriptToExecute = new NBitcoin.Script(item.output.WitScript[item.output.WitScript.PushCount - 2]);

            var sighash = TaprootSigHash.All;// TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

            var allSpendingOutputs = signingContext.Select(s => s.spendingOutput.TxOut).ToArray();
            var trxData = spender.PrecomputeTransactionData(allSpendingOutputs);
            var execData = new TaprootExecutionData(inputIndex, scriptToExecute.TaprootV1LeafHash) { SigHash = sighash };
            var hash = spender.GetSignatureHashTaproot(trxData, execData);

            var key = new Key(Encoders.Hex.DecodeData(investorPrivateKey));
            var sig = key.SignTaprootKeySpend(hash, sighash);

            if (!key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature))
            {
                throw new Exception();
            }

            List<Op> ops = new List<Op>();

            // the last 3 items on the stack are the fakesig, script and controlblock anything before that is the secrets

            ops.Add(Op.GetPushOp(sig.ToBytes()));

            foreach (var oppush  in item.output.WitScript.Pushes.Skip(1).Take(item.output.WitScript.Pushes.Count() - 3))
            {
                ops.Add(Op.GetPushOp(oppush));
            }

            ops.Add(Op.GetPushOp(scriptToExecute.ToBytes()));
            ops.Add(Op.GetPushOp(controBlock.ToBytes()));

            item.output.WitScript = new WitScript(ops.ToArray());
            inputIndex++;
        }

        if (!builder.Verify(spender, out TransactionPolicyError[] errors))
        {
            var sb = new StringBuilder();
            foreach (var transactionPolicyError in errors)
            {
                sb.AppendLine(transactionPolicyError.ToString());
            }

            throw new Exception(sb.ToString());
        }

        return network.CreateTransaction(spender.ToHex());
    }
}
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using NBitcoin;
using NBitcoin.Policy;
using System.Text;
using BitcoinAddress = Blockcore.NBitcoin.BitcoinAddress;
using FeeRate = Blockcore.NBitcoin.FeeRate;
using Key = NBitcoin.Key;
using Money = Blockcore.NBitcoin.Money;
using Network = Blockcore.Networks.Network;
using OutPoint = NBitcoin.OutPoint;
using RandomUtils = Blockcore.NBitcoin.RandomUtils;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using TransactionBuilder = Blockcore.Consensus.TransactionInfo.TransactionBuilder;
using TxIn = Blockcore.Consensus.TransactionInfo.TxIn;
using TxOut = Blockcore.Consensus.TransactionInfo.TxOut;
using uint256 = Blockcore.NBitcoin.uint256;
using WitScript = NBitcoin.WitScript;

public class InvestmentOperations
{
    private readonly IWalletOperations _walletOperations;

    public InvestmentOperations(IWalletOperations walletOperations)
    {
        _walletOperations = walletOperations;
    }

    /// <summary>
    /// This method will create a transaction with all the spending conditions
    /// based on the project investment metadata the transaction will be unsigned (it wont have any inputs yet)
    /// </summary>
    public Transaction CreateSeederTransaction(Network network,InvestorContext context, long totalInvestmentAmount)
    {
        Transaction investmentTransaction = network.Consensus.ConsensusFactory.CreateTransaction();

        // create the output and script of the project id 
        var angorFeeOutputScript = ScriptBuilder.GetAngorFeeOutputScript(context.ProjectInvestmentInfo.AngorFeeKey);
        var angorOutput = new TxOut(new Money(totalInvestmentAmount / 100), angorFeeOutputScript);
        investmentTransaction.AddOutput(angorOutput);

        // create the output and script of the investor pubkey script opreturn

        var opreturnScript = ScriptBuilder.GetSeederInfoScript(context.InvestorKey, context.InvestorSecretHash);
        var investorInfoOutput = new TxOut(new Money(0), opreturnScript);
        investmentTransaction.AddOutput(investorInfoOutput);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = context.ProjectInvestmentInfo.Stages
            .Select(_ => ScriptBuilder.BuildSeederScript(context.ProjectInvestmentInfo.FounderKey,
                context.InvestorKey,
                context.InvestorSecretHash, 
                _.NumberOfBLocks, 
                context.ProjectInvestmentInfo.ExpirationNumberOfBlocks));

        var stagesScripts = stagesScript.Select(scripts => 
            AngorScripts.CreateStageSeeder(network,scripts.founder,scripts.recover,scripts.endOfProject));


        var stagesOutputs = stagesScripts.Select((_, i) =>
            new TxOut(new Money(GetPercentageForStage(totalInvestmentAmount, i + 1)),
                new Script(_.ToBytes())));

        foreach (var stagesOutput in stagesOutputs)
        {
            investmentTransaction.AddOutput(stagesOutput);
        }

        return investmentTransaction;
    }

    private long GetPercentageForStage(long amount, int stage) //TODO move to interface 
    {
        return stage switch
        {
            1 => amount / 10,
            2 => (amount / 10) * 3,
            6 => throw new ArgumentOutOfRangeException(),
            _ => amount / 5
        };
    }
    
    public void SignInvestmentTransaction(Network network, InvestorContext context, Transaction transaction, WalletWords walletWords, List<UtxoDataWithPath> utxoDataWithPaths)
    {
        // We must use the NBitcoin lib because taproot outputs are non standard before taproot activated

        //var nbitcoinNetwork = NetworkMapper.Map(network);
        //var trx = NBitcoin.Transaction.Parse(transaction.ToHex(), nbitcoinNetwork);

        var coins = _walletOperations.GetUnspentOutputsForTransaction(walletWords, utxoDataWithPaths);

        var fees = _walletOperations.GetFeeEstimationAsync().Result;
        var fee = fees.First(f => f.Confirmations == 1);


        var incoins = coins.coins.Select(c => new NBitcoin.Coin(OutPoint.Parse(c.Outpoint.ToString()), new NBitcoin.TxOut(NBitcoin.Money.Satoshis(c.Amount.Satoshi), new NBitcoin.Script(c.ScriptPubKey.ToBytes()))));
        var inKeys = coins.keys.Select(k => new Key(k.ToBytes())).ToArray();

        var builder = new TransactionBuilder(network) // nbitcoinNetwork.CreateTransactionBuilder()
            .AddCoins(coins.coins)
            .AddKeys(coins.keys.ToArray())
            .SetChange(BitcoinAddress.Create(context.ChangeAddress, network))
            .ContinueToBuild(transaction)
            .SendEstimatedFees(new FeeRate(Money.Satoshis(fee.FeeRate)))
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

        context.TransactionHex = signTransaction.ToHex();
    }

    /// <summary>
    /// Allow the founder to spend the coins in a stage after the timelock has passed
    /// </summary>
    /// <exception cref="Exception"></exception>
    public Transaction SpendFounderStage(Network network, InvestorContext context, int stageNumber, Script founderRecieveAddress, string founderPrivateKey)
    {
        // We'll use the NBitcoin lib because its a taproot spend

        var fees = _walletOperations.GetFeeEstimationAsync().Result;
        var fee = fees.First(f => f.Confirmations == 1);

        var nbitcoinNetwork = NetworkMapper.Map(network);
        var trx = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);

        var spender = nbitcoinNetwork.CreateTransaction();

        var stageOutput = trx.Outputs.AsIndexedOutputs().ElementAt(stageNumber + 2);

        spender.Outputs.Add(stageOutput.TxOut.Value, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var scriptStages = ScriptBuilder.BuildSeederScript(context.ProjectInvestmentInfo.FounderKey,
            context.InvestorKey,
            context.InvestorSecretHash,
            context.ProjectInvestmentInfo.Stages[stageNumber].NumberOfBLocks,
            context.ProjectInvestmentInfo.ExpirationNumberOfBlocks);

        var controlBlock = AngorScripts.CreateControlBlockFounder(network, scriptStages.founder, scriptStages.recover, scriptStages.endOfProject);

        spender.Inputs.Add(new OutPoint(stageOutput.Transaction, stageOutput.N), null, null);

      
        NBitcoin.TransactionBuilder builder = nbitcoinNetwork.CreateTransactionBuilder()
            .AddCoin(new NBitcoin.Coin(trx, stageOutput.TxOut));

        var feeToReduce = builder.EstimateFees(spender, new NBitcoin.FeeRate(NBitcoin.Money.Satoshis(fee.FeeRate)));

        spender.Outputs[0].Value -= feeToReduce;

        var sighash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var hash = spender.GetSignatureHashTaproot(new[] { stageOutput.TxOut }, new TaprootExecutionData(0, new NBitcoin.Script(scriptStages.founder.ToBytes()).TaprootV1LeafHash) { SigHash = sighash });

        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));
        var sig = key.SignTaprootKeySpend(hash, sighash);

        if (!key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature))
        {
            throw new Exception();
        }

        spender.Inputs[0].WitScript = new WitScript(Op.GetPushOp(sig.ToBytes()), Op.GetPushOp(scriptStages.founder.ToBytes()), Op.GetPushOp(controlBlock.ToBytes()));

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

    public void RecoverInvestorFunds(InvestorContext context)
    {
        // allow an investor that acquired enough panel keys to recover their investment
    }

    public void RecoverEndOfProjectFunds(InvestorContext context)
    {
        // allow an investor that take back any coins left when the project end date has passed
    }
}
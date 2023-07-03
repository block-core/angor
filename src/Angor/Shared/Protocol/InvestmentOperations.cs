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
        var investorRedeemSecret = new uint256(RandomUtils.GetBytes(32));
        var opreturnScript = ScriptBuilder.GetSeederInfoScript(context.InvestorKey, investorRedeemSecret.ToString());
        var investorInfoOutput = new TxOut(new Money(0), opreturnScript);
        investmentTransaction.AddOutput(investorInfoOutput);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = context.ProjectInvestmentInfo.Stages
            .Select(_ => ScriptBuilder.BuildSeederScript(context.ProjectInvestmentInfo.FounderKey,
                context.InvestorKey, 
                investorRedeemSecret.ToString(), 
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
    
    public void SignInvestmentTransaction(InvestorContext context, Transaction transaction,List<UtxoDataWithPath> dataWithPaths)
    {
        var totalInOutputs = investmentTransaction.Outputs.Sum(s => s.Value);

        // var builder = new TransactionBuilder(network)
        //     .AddCoins(coins)
        //     .AddKeys(keys.ToArray())
        //     .SetChange(BitcoinWitPubKeyAddress.Create(sendInfo.ChangeAddress, network))
        //     .SendEstimatedFees(new FeeRate(Money.Coins(sendInfo.FeeRate)));
        
        // add the address and change output 
        var changeAmount = totalInOutputs - totalInInput;

        investmentTransaction.AddOutput(changeAmount, new Blockcore.NBitcoin.BitcoinWitPubKeyAddress(context.ChangeAddress, network).ScriptPubKey);

        // now we estimate the size of the fee
        var size = investmentTransaction.GetVirtualSize(network.Consensus.Options.WitnessScaleFactor);


        // Use the NBitcoin lib to sign and verify
        var builder = new TransactionBuilder(network)
            .AddKeys(inputs.Select(s => new Blockcore.NBitcoin.Key(Encoders.Hex.DecodeData(s.Key))).ToArray())
            .AddCoins(investmentTransaction)
          
            .SendEstimatedFees(new Blockcore.NBitcoin.FeeRate(feeRate));

        var signTransaction = builder.SignTransaction(investmentTransaction);

        var verifyresult = builder.Verify(signTransaction, out TransactionPolicyError[] result);

        if (result.Any())
        {
            StringBuilder sb = new();
            foreach (var policyError in result)
            {
                sb.AppendLine(policyError.ToString());
            }

            throw new Exception(sb.ToString());
        }

    }

    public Transaction SpendFounderStage(Network network, InvestorContext context, int stageNumber, Script founderRecieveAddress, string founderPrivateKey)
    {
        // allow the founder to spend the coins in a stage after the timelock has passed

        //var trx = network.Consensus.ConsensusFactory.CreateTransaction(context.TransactionHex);

        var nbitcoinNetwork = NetworkMapper.Map(network);
        var trx = NBitcoin.Transaction.Parse(context.TransactionHex, nbitcoinNetwork);
        
        var stageOutput = trx.Outputs[stageNumber + 2];

        var spender = NBitcoin.Network.TestNet.CreateTransaction();
        spender.Inputs.Add(new OutPoint(trx, stageNumber + 2));

        spender.Outputs.Add(stageOutput.Value, new NBitcoin.Script(founderRecieveAddress.ToBytes()));

        var sighash = TaprootSigHash.Single | TaprootSigHash.AnyoneCanPay;

        var hash = spender.GetSignatureHashTaproot(new[] { stageOutput }, new TaprootExecutionData(0) { SigHash = sighash });

        var key = new Key(Encoders.Hex.DecodeData(founderPrivateKey));
        var sig = key.SignTaprootKeySpend(hash, sighash);

        if (!key.CreateTaprootKeyPair().PubKey.VerifySignature(hash, sig.SchnorrSignature))
        {
            throw new Exception();
        }

        spender.Inputs[0].WitScript = new WitScript(Op.GetPushOp(sig.ToBytes()));

        NBitcoin.TransactionBuilder builder = NBitcoin.Network.TestNet.CreateTransactionBuilder();

        if(!builder.Verify(spender, out TransactionPolicyError[] errors))
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Angor.Shared;
using Angor.Shared.Protocol;
using NBitcoin;
using Money = Blockcore.NBitcoin.Money;
using Network = Blockcore.Networks.Network;
using RandomUtils = Blockcore.NBitcoin.RandomUtils;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;
using TxOut = Blockcore.Consensus.TransactionInfo.TxOut;
using uint256 = Blockcore.NBitcoin.uint256;

public class InvestmentOperations
{
    /// <summary>
    /// This method will create a transaction with all the spending conditions
    /// based on the project investment metadata the transaction will be unsigned (it wont have any inputs yet)
    /// </summary>
    public Transaction CreateInvestmentTransaction(Network network,InvestorContext context, long totalInvestmentAmount)
    {
        // create the output and script of the project id 
        var angorFeeOutputScript = ScriptBuilder.GetAngorFeeOutputScript(context.ProjectInvestmentInfo.AngorFeeKey);
        
        // create the output and script of the investor pubkey script opreturn
        var investorRedeemSecret = new uint256(RandomUtils.GetBytes(32));
        var opreturnScript = ScriptBuilder.GetSeederInfoScript(context.InvestorKey, investorRedeemSecret.ToString());

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = context.ProjectInvestmentInfo.Stages.Select(_ =>
            ScriptBuilder.BuildSeederScript(context.ProjectInvestmentInfo.FounderKey,
                context.InvestorKey, investorRedeemSecret.ToString(), _.NumberOfBLocks,
                context.ProjectInvestmentInfo.ExpirationNumberOfBlocks));

        var stagesKeys = stagesScript.Select(scripts => 
            AngorScripts.CreateStageSeeder(network,scripts.founder,scripts.recover,scripts.endOfProject));

        var angorOutput = new TxOut(new Money(totalInvestmentAmount / 100), angorFeeOutputScript);
        var investorInfoOutput = new TxOut(new Money(0), opreturnScript);

        var stagesOutputs = stagesKeys.Select((_, i) =>
            new TxOut(new Money(GetPercentageForStage(totalInvestmentAmount, i + 1)),
                new Script(_.ToBytes())));

        var t = new Transaction();
        t.AddOutput(angorOutput);
        t.AddOutput(investorInfoOutput);
        foreach (var stagesOutput in stagesOutputs)
        {
            t.AddOutput(stagesOutput);
        }

        return t;
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
    
    public void SignInvestmentTransaction(InvestorContext context)
    {
        // this method will add inputs to an investment transaction till the amount is satisfied 

        // add the address and change output 
    }

    public void SpendFounderStage(InvestorContext context, int stageNumber)
    {
        // allow the founder to spend the coins in a stage after the timelock has passed
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
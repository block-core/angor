using System;
using System.Collections.Generic;
using Blockcore.Consensus.TransactionInfo;

public class InvestmentOperations
{
    /// <summary>
    /// This method will create a transaction with all the spending conditions
    /// based on the project investment metadata the transaction will be unsigned (it wont have any inputs yet)
    /// </summary>
    public void CreateInvestmentTransaction(InvestorContext context)
    {
        // create the output and script of the project id 

        // create the output and script of the investor pubkey script opreturn

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage

        // in-stage : create the script for the founder to spend the stage coins

        // in-stage : create the script for each panel plus investor key

        // in-stage : create the end date timelock

        // in-stage : bundle all the stage scripts in to a taproot commitment 

        // add each stage as output

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
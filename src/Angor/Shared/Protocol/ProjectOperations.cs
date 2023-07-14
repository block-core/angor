using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared.Protocol;

public class ProjectOperations
{
    public static Transaction CreateNewProjectTransaction(Network network,string founderKey, Script angorKey, long angorFeeSatoshis)
    {
        var projectStartTransaction = network.Consensus.ConsensusFactory.CreateTransaction();
        
        // create the output and script of the investor pubkey script opreturn
        var investorInfoOutput = new TxOut(new Money(angorFeeSatoshis), angorKey);
        projectStartTransaction.AddOutput(investorInfoOutput);
        
        // create the output and script of the project id 
        var angorFeeOutputScript = ScriptBuilder.GetProjectStartScript(founderKey);
        var founderOPReturnOutput = new TxOut(new Money(0), angorFeeOutputScript);
        projectStartTransaction.AddOutput(founderOPReturnOutput);

        return projectStartTransaction;
    }
}
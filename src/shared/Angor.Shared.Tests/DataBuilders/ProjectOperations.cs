using NBitcoin;
using Angor.Shared.Networks;

namespace Angor.Test.DataBuilders;

public class ProjectOperations
{
    public static Transaction CreateNewProjectTransaction(AngorNetwork network,string founderKey, Script angorKey, long angorFeeSatoshis)
    {
        var projectStartTransaction = network.CreateTransaction();
        
        // create the output and script of the project id
        var investorInfoOutput = new TxOut(new Money(angorFeeSatoshis), angorKey);
        projectStartTransaction.Outputs.Add(investorInfoOutput);

        // todo: here we should add the hash of the project data as opreturn

        // create the output and script of the investor pubkey script opreturn
        var angorFeeOutputScript = ScriptBuilder.GetProjectStartScript(founderKey);
        var founderOPReturnOutput = new TxOut(new Money(0), angorFeeOutputScript);
        projectStartTransaction.Outputs.Add(founderOPReturnOutput);

        return projectStartTransaction;
    }
}

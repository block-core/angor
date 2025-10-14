using Angor.Shared.Models;

namespace Angor.Shared.Utilities;

public static class TransactionExtension
{
    public static long SumAngorAmount(this Blockcore.Consensus.TransactionInfo.Transaction investmentTransaction, ProjectInfo projectInfo)
    {
        var totalInvestmentAmount = investmentTransaction.Outputs.Skip(2).Take(projectInfo.Stages.Count).Sum(o => o.Value);

        return totalInvestmentAmount;
    }

    public static long SumAngorAmount(this NBitcoin.Transaction investmentTransaction, ProjectInfo projectInfo)
    {
        var totalInvestmentAmount = investmentTransaction.Outputs.Skip(2).Take(projectInfo.Stages.Count).Sum(o => o.Value);

        return totalInvestmentAmount;
    }
}

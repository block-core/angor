using Angor.Shared.Models;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.Utilities;

public static class PenaltyThresholdHelper
{
    public static bool IsInvestmentAbovePenaltyThreshold(ProjectInfo projectInfo, long investmentAmount)
    {
        // Penalty threshold only applies to Fund projects.
        // Callers must check the project type before invoking this method.
        if (projectInfo.ProjectType != ProjectType.Fund)
        {
            throw new InvalidOperationException(
                $"Penalty threshold check is only valid for Fund projects, but was called for project type '{projectInfo.ProjectType}'.");
        }

        if (!projectInfo.PenaltyThreshold.HasValue)
        {
            return true;
        }

        return investmentAmount > projectInfo.PenaltyThreshold.Value;
    }

    public static DateTime? GetExpiryDateOverride(ProjectInfo projectInfo, long investmentAmount)
    {
        if (projectInfo.ProjectType != ProjectType.Fund)
        {
            return null;
        }

        if (!IsInvestmentAbovePenaltyThreshold(projectInfo, investmentAmount))
        {
            return projectInfo.StartDate.Date;
        }

        return null;
    }

    public static long GetTotalInvestmentAmount(Transaction investmentTransaction)
    {
        return investmentTransaction.GetTotalInvestmentAmount();
    }

    public static bool IsInvestmentAbovePenaltyThreshold(ProjectInfo projectInfo, Transaction investmentTransaction)
    {
        var totalInvestmentAmount = GetTotalInvestmentAmount(investmentTransaction);
        return IsInvestmentAbovePenaltyThreshold(projectInfo, totalInvestmentAmount);
    }

    public static DateTime? GetExpiryDateOverride(ProjectInfo projectInfo, Transaction investmentTransaction)
    {
        var totalInvestmentAmount = GetTotalInvestmentAmount(investmentTransaction);
        return GetExpiryDateOverride(projectInfo, totalInvestmentAmount);
    }
}

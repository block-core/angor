using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Projects.Dtos;

public class DynamicStageDto
{
    public int StageIndex { get; set; }
    public DateTime ReleaseDate { get; set; }
    public long TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public int UnspentTransactionCount { get; set; }
    public long UnspentAmount { get; set; }
    public bool IsReleased => ReleaseDate <= DateTime.UtcNow;
    public string Status { get; set; } = "";

    public static string ResolveItemStatus(Domain.StageDataTrx item, DateTime stageDate)
    {
        if (!item.IsSpent)
        {
            return stageDate > DateTime.UtcNow ? "Unspent" : "Available";
        }

        if (item.ProjectScriptType?.ScriptType != null)
        {
            return item.ProjectScriptType.ScriptType switch
            {
                ProjectScriptTypeEnum.Founder => "Spent by Founder",
                ProjectScriptTypeEnum.InvestorWithPenalty => "Recovered to Penalty",
                ProjectScriptTypeEnum.InvestorNoPenalty => "Recovered by Investor",
                ProjectScriptTypeEnum.EndOfProject => "Recovered by Investor",
                _ => "Pending"
            };
        }

        return item.SpentType switch
        {
            "founder" => "Spent by Founder",
            "investor" => "Recovered by Investor",
            "pending" => "Pending",
            _ => "Pending"
        };
    }
}

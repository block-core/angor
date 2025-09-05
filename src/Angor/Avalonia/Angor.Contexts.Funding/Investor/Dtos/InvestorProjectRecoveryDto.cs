using Angor.Shared.Models;

namespace Angor.Contexts.Funding.Investor.Dtos;

public class InvestorProjectRecoveryDto
{
    public string ProjectIdentifier { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int PenaltyDays { get; set; }

    public long TotalSpendable { get; set; }
    public long TotalInPenalty { get; set; }

    public bool CanRecover { get; set; }
    public bool CanRelease { get; set; }
    public bool EndOfProject { get; set; }

    public string? ExplorerLink { get; set; }

    public List<InvestorStageItemDto> Items { get; set; } = new();
}

public class InvestorStageItemDto
{
    public int StageIndex { get; set; }
    public long Amount { get; set; }
    public bool IsSpent { get; set; }
    public string Status { get; set; } = string.Empty;
    public ProjectScriptTypeEnum ScriptType { get; set; } = ProjectScriptTypeEnum.Unknown;
}

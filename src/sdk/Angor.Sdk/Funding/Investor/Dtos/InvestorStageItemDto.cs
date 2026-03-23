using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Investor.Dtos;

public class InvestorStageItemDto
{
    public int StageIndex { get; set; }
    public long Amount { get; set; }
    public bool IsSpent { get; set; }
    public string Status { get; set; } = string.Empty;
    public ProjectScriptTypeEnum ScriptType { get; set; } = ProjectScriptTypeEnum.Unknown;
}
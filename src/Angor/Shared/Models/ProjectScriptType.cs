namespace Angor.Shared.Models;

public class ProjectScriptType
{
    public ProjectScriptTypeEnum ScriptType { get; set; }
}

public enum ProjectScriptTypeEnum
{
    Unknown,
    Founder,
    InvestorWithPenalty,
    InvestorNoPenalty,
    EndOfProject
}
using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Projects.Domain;

public class StageDataTrx
{
    public string Trxid = string.Empty;
    public int Outputindex;
    public string OutputAddress = string.Empty;
    public int StageIndex;
    public long Amount;
    public bool IsSpent;
    public string SpentType = string.Empty;
    public string InvestorNpub = string.Empty;
    public string InvestorPublicKey = string.Empty;
    public ProjectScriptType ProjectScriptType = null!;
    public DateTime? DynamicReleaseDate { get; set; }
    public byte? PatternIndex { get; set; }
    public DateTime? InvestmentStartDate { get; set; }
    public decimal? AmountPercentage { get; set; }
}
using Angor.Shared.Models;

namespace Angor.Contexts.Funding.Projects.Domain;

public class StageDataTrx
{
    public string Trxid;
    public int Outputindex;
    public string OutputAddress;
    public int StageIndex;
    public long Amount;
    public bool IsSpent;
    public string SpentType;
    public string InvestorNpub;
    public string InvestorPublicKey;
    public ProjectScriptType ProjectScriptType;
    public DateTime? DynamicReleaseDate { get; set; }
    public byte? PatternIndex { get; set; }
    public DateTime? InvestmentStartDate { get; set; }
    public decimal? AmountPercentage { get; set; }
}
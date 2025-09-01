using Angor.Shared.Models;

namespace Angor.Contexts.Funding.Projects.Domain;

public class StageDataTrx
{
    public string Trxid;
    public int Outputindex;
    public string OutputAddress;
    public long Amount;
    public bool IsSpent;
    public string SpentType;  // "founder" or "investor"
    public string InvestorNpub;  // Optional, can be null
    public string InvestorPublicKey;
    public ProjectScriptType ProjectScriptType;
}
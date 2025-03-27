using Angor.Shared.Models;

namespace Angor.Client.Models;

public class ApprovedInvestment
{
    public string InvestorNPub { get; set; }
    public string RequestEventId { get; set; }
    public DateTime ApprovalTime { get; set; }
}
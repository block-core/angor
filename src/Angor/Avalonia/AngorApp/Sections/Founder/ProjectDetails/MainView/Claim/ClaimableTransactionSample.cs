using Angor.Contexts.Funding.Founder.Dtos;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;

public class ClaimableTransactionSample : IClaimableTransaction
{
    public IAmountUI Amount { get; set; } = new AmountUI(100000); 
    public string Address { get; set; }  = "bc1qexampleaddress"; 
    public ClaimStatus ClaimStatus { get; set; } = ClaimStatus.Unspent;
}
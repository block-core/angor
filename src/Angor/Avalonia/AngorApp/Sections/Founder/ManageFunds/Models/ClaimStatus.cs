namespace AngorApp.Sections.Founder.ManageFunds.Models;

public enum ClaimStatus
{
    Invalid = 0,
    Unspent,
    Pending,
    SpentByFounder,
    WithdrawByInvestor
}
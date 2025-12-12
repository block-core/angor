namespace Angor.Sdk.Funding.Founder.Dtos;

public enum ClaimStatus
{
    Invalid = 0,
    Unspent,
    Pending,
    Locked,
    SpentByFounder,
    WithdrawByInvestor
}
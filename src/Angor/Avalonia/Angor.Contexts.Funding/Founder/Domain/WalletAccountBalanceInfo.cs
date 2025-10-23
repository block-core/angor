using Angor.Shared.Models;

namespace Angor.Contexts.Funding.Founder.Domain;

public class WalletAccountBalanceInfo()
{
    public required AccountBalanceInfo AccountBalanceInfo { get; set; }
    public required string WalletId { get; set; } 
}
using Angor.Shared.Models;

namespace Angor.Sdk.Common;

public class WalletAccountBalanceInfo
{
    public required AccountBalanceInfo AccountBalanceInfo { get; set; }
    public required string WalletId { get; set; } 
}


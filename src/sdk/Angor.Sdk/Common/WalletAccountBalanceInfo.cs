using Angor.Shared.Models;

using Angor.Primitives;

namespace Angor.Sdk.Common;

public class WalletAccountBalanceInfo
{
    public required AccountBalanceInfo AccountBalanceInfo { get; set; }
    public required string WalletId { get; set; } 
}


using Angor.Shared.Models;
using Angor.Shared.Protocol;

namespace Angor.Contests.CrossCutting;

public class DerivedProjectKeys
{
    public required string WalletId { get; set; }
    public required List<FounderKeys> Keys { get; set; }
}

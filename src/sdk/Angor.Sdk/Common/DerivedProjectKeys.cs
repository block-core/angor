using Angor.Shared.Models;
using Angor.Shared.Protocol;

using Angor.Primitives;

namespace Angor.Sdk.Common;

public class DerivedProjectKeys
{
    public required string WalletId { get; set; }
    public required List<FounderKeys> Keys { get; set; }
}

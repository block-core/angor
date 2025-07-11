using Zafiro.Avalonia.Misc;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ManageFunds;

public interface IClaimableStage
{
    public int ClaimableTransactions { get; }
    public IEnumerable<IClaimableTransaction> Transactions { get; }
    public IAmountUI ClaimableAmount { get; }
    IEnhancedCommand Claim { get; }
    ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }
}
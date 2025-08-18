using Zafiro.Avalonia.Misc;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public interface IClaimableStage
{
    public int ClaimableTransactionsCount { get; }
    public IEnumerable<IClaimableTransaction> Transactions { get; }
    public IAmountUI ClaimableAmount { get; }
    IEnhancedCommand<Result> Claim { get; }
    ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }
}
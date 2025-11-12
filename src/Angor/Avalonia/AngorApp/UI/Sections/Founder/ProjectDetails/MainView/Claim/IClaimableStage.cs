using Zafiro.Avalonia.Misc;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Claim;

public interface IClaimableStage
{
    public int ClaimableTransactionsCount { get; }
    public IEnumerable<IClaimableTransaction> Transactions { get; }
    public IAmountUI ClaimableAmount { get; }
    IEnhancedCommand<Maybe<Result>> Claim { get; }
    ReactiveSelection<IClaimableTransaction, string> ReactiveSelection { get; }
}
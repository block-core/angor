using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public interface IReleaseFundsViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; }
    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }
}
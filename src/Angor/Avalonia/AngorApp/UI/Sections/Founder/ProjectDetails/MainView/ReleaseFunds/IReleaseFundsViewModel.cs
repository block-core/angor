using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public interface IReleaseFundsViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; }
    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }
    ReactiveCommand<Unit, Result<List<IUnfundedProjectTransaction>>> RefreshTransactions { get; }
}

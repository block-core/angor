using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public class ReleaseFundsViewModelSample : IReleaseFundsViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; } = (List<IUnfundedProjectTransaction>)
    [
        new UnfundedProjectTransactionSample
        {
            Approved = DateTime.Now,
            Released = DateTime.Now,
            Arrived = DateTime.Now,
        },

        new UnfundedProjectTransactionSample
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        },

        new UnfundedProjectTransactionSample
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        }
    ];

    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }
    public ReactiveCommand<Unit, Result<List<IUnfundedProjectTransaction>>> RefreshTransactions { get; } =
        ReactiveCommand.Create(() => Result.Success(new List<IUnfundedProjectTransaction>()));
}

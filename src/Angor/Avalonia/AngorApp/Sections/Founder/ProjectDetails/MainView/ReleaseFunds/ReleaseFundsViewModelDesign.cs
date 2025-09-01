using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public class ReleaseFundsViewModelDesign : IReleaseFundsViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; } = (List<IUnfundedProjectTransaction>)
    [
        new UnfundedProjectTransactionDesign
        {
            Approved = DateTime.Now,
            Released = DateTime.Now,
            Arrived = DateTime.Now,
        },

        new UnfundedProjectTransactionDesign
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        },

        new UnfundedProjectTransactionDesign
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        }
    ];

    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }
}
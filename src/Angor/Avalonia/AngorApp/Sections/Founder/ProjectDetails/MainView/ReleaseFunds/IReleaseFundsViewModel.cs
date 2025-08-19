namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public interface IReleaseFundsViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; }
}
namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public interface IUnfundedProjectViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; }
}
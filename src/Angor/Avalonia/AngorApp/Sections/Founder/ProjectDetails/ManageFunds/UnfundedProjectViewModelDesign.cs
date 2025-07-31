namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public class UnfundedProjectViewModelDesign : IUnfundedProjectViewModel
{
    public IEnumerable<IUnfundedProjectTransaction> Transactions { get; } = new List<IUnfundedProjectTransaction>()
    {
        new UnfundedProjectTransactionDesign()
        {
            Approved = DateTime.Now,
            Released = DateTime.Now,
            Arrived = DateTime.Now,
        },
        new UnfundedProjectTransactionDesign()
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        },
        new UnfundedProjectTransactionDesign()
        {
            Approved = DateTime.Now,
            Arrived = DateTime.Now,
        }
    };
}
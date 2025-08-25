namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;

public interface IApproveInvestmentsViewModel : IDisposable
{
    public IEnumerable<IInvestmentViewModel> Investments { get; }
    ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
}
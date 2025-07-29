namespace AngorApp.Sections.Founder.ProjectDetails.Investments;

public interface IProjectInvestmentsViewModel : IDisposable
{
    public IEnumerable<IInvestmentViewModel> Investments { get; }
    ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
}
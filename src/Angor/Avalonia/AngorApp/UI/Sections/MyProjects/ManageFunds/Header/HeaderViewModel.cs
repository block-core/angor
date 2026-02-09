namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Header;

public class HeaderViewModel(IFullProject fullProject, IEnhancedCommand<Result<IFullProject>> Refresh) : IHeaderViewModel
{
    public IEnhancedCommand<Result<IFullProject>> Refresh { get; } = Refresh;
    public string ProjectTitle => fullProject.Name;
    public decimal Progress => fullProject.RaisedAmount.Btc / fullProject.TargetAmount.Btc;
    public IAmountUI Raised => fullProject.RaisedAmount;
}
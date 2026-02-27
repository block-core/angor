using AngorApp.UI.Sections.MyProjects.ManageFunds;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Header;

public class HeaderViewModel(IManageFundsProject project, IEnhancedCommand refresh) : IHeaderViewModel
{
    public IEnhancedCommand Refresh { get; } = refresh;
    public string ProjectTitle => project.Name;
    public decimal Progress => project.RaisedAmount.Btc / project.TargetAmount.Btc;
    public IAmountUI Raised => project.RaisedAmount;
}

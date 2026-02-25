using AngorApp.UI.Sections.MyProjects.ManageFunds;
using AngorApp.UI.Shared;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Header;

public class HeaderViewModel(IManageFundsProject project, IEnhancedCommand refresh) : IHeaderViewModel
{
    public IEnhancedCommand Refresh { get; } = refresh;
    public string ProjectTitle => project.Name;
    public decimal Progress => project.RaisedAmount.RatioOrZero(project.TargetAmount);
    public IAmountUI Raised => project.RaisedAmount;
}

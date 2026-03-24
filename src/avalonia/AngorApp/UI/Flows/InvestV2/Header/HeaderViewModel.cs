using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.InvestV2.Header;

public class HeaderViewModel(IFullProject fullProject) : IHeaderViewModel
{
    public string ProjectTitle => fullProject.Name;
    public decimal Progress => fullProject.RaisedAmount.RatioOrZero(fullProject.TargetAmount);
    public IAmountUI Raised => fullProject.RaisedAmount;
}

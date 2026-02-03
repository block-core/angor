namespace AngorApp.UI.Flows.InvestV2.Header;

public class HeaderViewModel(IFullProject fullProject) : IHeaderViewModel
{
    public string ProjectTitle => fullProject.Name;
    public decimal Progress => fullProject.RaisedAmount.Btc / fullProject.TargetAmount.Btc;
    public IAmountUI Raised => fullProject.RaisedAmount;
}
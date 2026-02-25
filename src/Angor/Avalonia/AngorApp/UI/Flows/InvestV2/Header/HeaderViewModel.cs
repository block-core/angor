namespace AngorApp.UI.Flows.InvestV2.Header;

public class HeaderViewModel(IFullProject fullProject) : IHeaderViewModel
{
    public string ProjectTitle => fullProject.Name;
    public decimal Progress => fullProject.TargetAmount.Sats != 0 ? fullProject.RaisedAmount.Btc / fullProject.TargetAmount.Btc : 0;
    public IAmountUI Raised => fullProject.RaisedAmount;
}
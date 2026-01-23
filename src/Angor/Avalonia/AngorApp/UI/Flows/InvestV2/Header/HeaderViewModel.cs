namespace AngorApp.UI.Flows.InvestV2.Header;

public class HeaderViewModel : IHeaderViewModel
{
    public string ProjectTitle { get; } = "Sample Project";
    public decimal Progress { get; } = 0.5m;
    public IAmountUI Raised { get; } = AmountUI.FromBtc(0.1234m);
}
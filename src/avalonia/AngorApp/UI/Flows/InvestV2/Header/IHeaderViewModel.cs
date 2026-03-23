namespace AngorApp.UI.Flows.InvestV2.Header;

public interface IHeaderViewModel
{
    public string ProjectTitle { get; }
    public decimal Progress { get; }
    public IAmountUI Raised { get; }
}
namespace AngorApp.UI.Flows.InvestV2.Footer;

public interface IFooterViewModel
{
    public IAmountUI AmountToInvest { get; }
    public int NumberOfReleases { get; }
    public IEnhancedCommand<bool> Invest { get; }
}
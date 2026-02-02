namespace AngorApp.UI.Flows.InvestV2.Footer;

public class FooterViewModelSample : IFooterViewModel
{
    public IAmountUI AmountToInvest { get; } = AmountUI.FromBtc(0.4m);
    public int NumberOfReleases { get; } = 1;
    public IEnhancedCommand<bool> Invest { get; }
}
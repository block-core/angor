using Zafiro.Avalonia.Dialogs;

using AngorApp.UI.Flows.InvestV2.PaymentSelector;
using AngorApp.UI.Shell;

namespace AngorApp.UI.Flows.InvestV2.Footer;

public class FooterViewModel : IFooterViewModel
{
    private readonly UIServices uiServices;
    public IAmountUI AmountToInvest { get; } = AmountUI.FromBtc(0.4m);
    public int NumberOfReleases { get; } = 1;
    public IEnhancedCommand<bool> Invest { get; }

    public FooterViewModel(UIServices uiServices, IShellViewModel shell)
    {
        this.uiServices = uiServices;
        Invest = ReactiveCommand.CreateFromTask(() => uiServices.Dialog.Show(new PaymentSelectorViewModel(uiServices, shell), "Select Wallet", (model, closeable) => model.Options(closeable))).Enhance();
    }
}
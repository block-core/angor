using System.Windows.Input;
using AngorApp.Model;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Wallet.Operate.Send;
using AngorApp.Services;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using TransactionPreviewViewModel = AngorApp.UI.Controls.Common.TransactionPreview.TransactionPreviewViewModel;

namespace AngorApp.Sections.Wallet.Operate;

public class WalletViewModel(IWallet wallet, UIServices uiServices) : ReactiveObject, IWalletViewModel
{
    public IWallet Wallet => wallet;

    public ICommand Send => ReactiveCommand.CreateFromTask<bool>(() =>
    {
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(wallet))
            .Then(model => new TransactionPreviewViewModel(wallet, new Destination("Test", model.Amount!.Value, SampleData.TestNetBitcoinAddress), uiServices))
            .Then(_ => new SuccessViewModel("Transaction sent!", "Success"))
            .Build();

        return uiServices.Dialog.Show(wizard, "Send", closeable => wizard.OptionsForCloseable(closeable));
    });
}
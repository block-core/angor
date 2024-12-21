using System.Windows.Input;
using AngorApp.Common;
using AngorApp.Common.TransactionPreview;
using AngorApp.Model;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Wallet.Send;
using AngorApp.Services;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using TransactionPreviewViewModel = AngorApp.Common.TransactionPreview.TransactionPreviewViewModel;

namespace AngorApp.Sections.Wallet;

public class WalletViewModel(IWallet wallet, UIServices uiServices) : ReactiveObject, IWalletViewModel
{
    public IWallet Wallet => wallet;

    public ICommand Send => ReactiveCommand.CreateFromTask(() =>
    {
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(wallet))
            .Then(model => new TransactionPreviewViewModel(wallet, new Destination("Test", model.Amount!.Value, SampleData.TestNetBitcoinAddress), uiServices))
            .Then(_ => new SuccessViewModel("Transaction sent!"))
            .Build();

        return uiServices.Dialog.Show(wizard, "Send", closeable => wizard.OptionsForCloseable(closeable));
    });
}

public interface IWalletViewModel
{
    public IWallet Wallet { get; }
    public ICommand Send { get; }
}

public class WalletViewModelDesign : IWalletViewModel
{
    public IWallet Wallet { get; set; } = new WalletDesign();
    public ICommand Send { get; }
}
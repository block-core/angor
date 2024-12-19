using System.Threading.Tasks;
using System.Windows.Input;
using AngorApp.Common;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Browse.Details.Invest.TransactionPreview;
using AngorApp.Sections.Wallet.Send;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Wallet;

public class WalletViewModel(IWallet wallet, UIServices uiServices) : ReactiveObject, IWalletViewModel
{
    public IWallet Wallet => wallet;

    public ICommand Send => ReactiveCommand.CreateFromTask(() =>
    {
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(wallet))
            .Then(model => new TransactionPreviewViewModel(wallet, new ProjectDesign(), uiServices, model.Amount!.Value))
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

public class TransactionDesign : ITransaction
{
    public string Address { get; set; }
    public decimal Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }

    public async Task<Result> Broadcast()
    {
        await Task.Delay(4000);
        //return Result.Failure("Catastrophe!");
        return Result.Success();
    }
}

public interface IWallet
{
    public IEnumerable<ITransaction> History { get; }
    decimal? Balance { get; set; }
    Task<ITransaction> CreateTransaction(decimal amount, string address);
    Result IsAddressValid(string address);
    public BitcoinAddressValidator.BitcoinNetwork Network { get;  }
}

public interface ITransaction
{
    public string Address { get; }
    public decimal Amount { get; }
    public string Path { get; }
    public int UtxoCount { get; }
    public string ViewRawJson { get; }
    Task<Result> Broadcast();
}
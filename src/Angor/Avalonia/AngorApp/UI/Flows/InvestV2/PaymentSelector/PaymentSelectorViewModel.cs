using AngorApp.UI.Sections.Wallet;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;
using AngorApp.UI.Flows.InvestV2.Invoice;
using AngorApp.UI.Flows.InvestV2.BackupWallet;
using AngorApp.UI.Flows.InvestV2.InvestmentResult;
using AngorApp.UI.Shell;

namespace AngorApp.UI.Flows.InvestV2.PaymentSelector;

public partial class PaymentSelectorViewModel : ReactiveObject, IPaymentSelectorViewModel, IHaveTitle
{
    private readonly UIServices uiServices;

    public PaymentSelectorViewModel(UIServices uiServices, IShellViewModel shell)
    {
        this.uiServices = uiServices;
        this.shell = shell;
    }

    [Reactive] private IWallet? selectedWallet;
    private readonly IShellViewModel shell;
    public IAmountUI AmountToInvest { get; } = AmountUI.FromBtc(0.5m);

    public IEnumerable<IWallet> Wallets { get; } =
    [
        new WalletSample { Name = "Fat Wallet", Balance = AmountUI.FromBtc(100) },
        new WalletSample { Name = "Savings Wallet", Balance = AmountUI.FromBtc(12) },
        new WalletSample { Name = "Tipping Wallet", Balance = AmountUI.FromBtc(0.5) }
    ];

    public IObservable<string> Title { get; } = Observable.Return("Select Payment Method");

    public IEnumerable<IOption> Options(ICloseable closeable) =>
    [
        PayWithWalletOption(closeable),
        GenerateInvoiceOption(closeable)
    ];

    private Option GenerateInvoiceOption(ICloseable closeable)
    {
        var command = EnhancedCommand.Create(async () =>
        {
            closeable.Close();
            await uiServices.Dialog.Show(
                new InvoiceViewModel(),
                "Pay Invoice to Invest",
                (model, closeable) =>
                [
                    new Option(
                        "Next",
                        EnhancedCommand.Create(
                            () =>
                            {
                                closeable.Close();
                                return uiServices.Dialog.Show(
                                    new BackupWalletViewModel(uiServices),
                                    "Backup Your Account",
                                    (model, c) => model.Options(c, shell));
                            },
                            model.IsValid),
                        new Settings() { IsVisible = model.IsValid })
                ]);
        });

        return new Option("Generate Invoice Instead", command, new Settings());
    }

    private Option PayWithWalletOption(ICloseable closeable)
    {
        var title = this.WhenAnyValue(viewModel => viewModel.SelectedWallet)
                        .Select(wallet => wallet.AsMaybe().Match(
                                    x => "Pay with " + x.Name,
                                    () => "Choose Wallet"));

        var command = EnhancedCommand.Create(
            async () =>
            {
                closeable.Close();
                await uiServices.Dialog.Show(new InvestResultViewModel(shell), "Investment Completed", (model, c) => model.Options(c));
            },
            this.WhenAnyValue(viewModel => viewModel.SelectedWallet).NotNull());

        return new Option(title, command, new Settings());
    }
}
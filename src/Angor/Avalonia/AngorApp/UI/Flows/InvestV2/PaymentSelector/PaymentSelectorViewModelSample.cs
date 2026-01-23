using AngorApp.UI.Sections.Wallet;

namespace AngorApp.UI.Flows.InvestV2.PaymentSelector;

public partial class PaymentSelectorViewModelSample : ReactiveObject, IPaymentSelectorViewModel
{
    [Reactive] private IWallet? selectedWallet;
    public IAmountUI AmountToInvest { get; } = AmountUI.FromBtc(0.5m);
    public IEnumerable<IWallet> Wallets { get; } = [
        new WalletSample() { Name = "Fat Wallet", Balance = AmountUI.FromBtc(100) },
        new WalletSample() { Name = "Savings Wallet", Balance = AmountUI.FromBtc(12) },
        new WalletSample() { Name = "Tipping Wallet", Balance = AmountUI.FromBtc(0.5) }
    ];
}
using AngorApp.UI.Flows.InvestV2.Model;

namespace AngorApp.UI.Flows.InvestV2.PaymentSelector;

public interface IPaymentSelectorViewModel
{
    public IAmountUI AmountToInvest { get; }
    public IEnumerable<IWallet> Wallets { get; }
    IWallet? SelectedWallet { get; set; }
}
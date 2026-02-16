using Branta.V2.Models;

namespace AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount;

public class AddressAndAmountViewModelSample : IAddressAndAmountViewModel
{
    public IObservable<bool> IsValid { get; } = Observable.Return(true);
    public IObservable<bool> IsBusy { get; } = Observable.Return(false);
    public bool AutoAdvance { get; } = false;
    public long? Amount { get; set; } = 100;
    public string? Address { get; set; } = "testaddress";
    public IAmountUI WalletBalance { get; } = new AmountUI(113000);
    public bool ShowBrantaVerification { get; set; } = false;
    public bool BrantaLoading { get; set; } = false;
    public bool BrantaPaymentsFound { get; set; } = false;
    public List<Payment>? BrantaPayments { get; set; } = [];
}
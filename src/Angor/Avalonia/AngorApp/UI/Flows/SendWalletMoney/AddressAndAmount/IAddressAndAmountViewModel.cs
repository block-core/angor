using Branta.V2.Models;

namespace AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount;

public interface IAddressAndAmountViewModel 
{
    public long? Amount { get; set; }
    public string? Address { get; set; }
    public IAmountUI WalletBalance { get; }
    public bool ShowBrantaVerification { get; set; }
    public bool BrantaLoading { get; set; }
    public bool BrantaPaymentsFound { get; set; }
    public List<Payment>? BrantaPayments { get; set; }
}
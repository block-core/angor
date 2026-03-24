namespace AngorApp.UI.Flows.SendWalletMoney.AddressAndAmount;

public interface IAddressAndAmountViewModel 
{
    public long? Amount { get; set; }
    public string? Address { get; set; }
    public IAmountUI WalletBalance { get; }
}
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Operate.Send.AddressAndAmount;

public interface IAddressAndAmountViewModel : IStep
{
    public long? Amount { get; set; }
    public string? Address { get; set; }
    public IAmountUI WalletBalance { get; }
}
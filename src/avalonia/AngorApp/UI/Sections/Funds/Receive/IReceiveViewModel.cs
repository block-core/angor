namespace AngorApp.UI.Sections.Funds.Receive;

public interface IReceiveViewModel
{
    IWallet Wallet { get; }
    public IObservable<string> Address { get; }
}
using AngorApp.UI.Sections.Funds.Accounts;
using AngorApp.UI.Sections.Funds.Receive;
using AngorApp.UI.Sections.Wallet;

namespace AngorApp
{
    public class ReceiveViewModelSample : IReceiveViewModel
    {
        public IWallet Wallet { get; } = new WalletSample();
        public IObservable<string> Address { get; } = Observable.Return("address");
    }
}
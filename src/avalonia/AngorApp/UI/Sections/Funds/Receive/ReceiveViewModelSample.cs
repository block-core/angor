using AngorApp.UI.Shared.Samples;

namespace AngorApp.UI.Sections.Funds.Receive
{
    public class ReceiveViewModelSample : IReceiveViewModel
    {
        public IWallet Wallet { get; } = new WalletSample();
        public IObservable<string> Address { get; } = Observable.Return("address");
    }
}
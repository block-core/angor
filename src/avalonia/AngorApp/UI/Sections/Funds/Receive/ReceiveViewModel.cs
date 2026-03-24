using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Funds.Receive
{
    public class ReceiveViewModel(IWallet wallet) : IReceiveViewModel
    {
        public IWallet Wallet { get; } = wallet;
        public IObservable<string> Address => Wallet.GetReceiveAddress.Successes();
    }
}
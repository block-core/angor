using AngorApp.UI.Sections.Funds.Receive;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class Account : IAccount
    {
        private readonly UIServices uiServices;

        public Account(IWallet wallet, UIServices uiServices)
        {
            this.uiServices = uiServices;
            Wallet = wallet;
        }

        public IWallet Wallet { get; }
        public IEnhancedCommand Send => Wallet.Send;
        public IEnhancedCommand Receive => EnhancedCommand.Create(() => uiServices.Dialog.ShowOk(new ReceiveViewModel(Wallet), "Receive Bitcoin"));
        public IEnhancedCommand ShowDetails { get; }
    }
}
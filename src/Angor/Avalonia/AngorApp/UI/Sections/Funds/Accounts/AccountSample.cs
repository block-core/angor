using AngorApp.UI.Sections.Wallet;

namespace AngorApp.UI.Sections.Funds.Accounts
{
    public class AccountSample : IAccount
    {
        public IWallet Wallet { get; set; } = new WalletSample();
        public IEnhancedCommand Send { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand Receive { get; }  = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand ShowDetails { get; } = EnhancedCommand.Create(() => { });
    }
}
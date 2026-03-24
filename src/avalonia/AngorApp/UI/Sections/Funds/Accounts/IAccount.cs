namespace AngorApp.UI.Sections.Funds.Accounts
{
    public interface IAccount
    {
        public IWallet Wallet { get;  }
        public IEnhancedCommand Send { get; }
        public IEnhancedCommand Receive { get; }
        public IEnhancedCommand ShowDetails { get; }
    }
}
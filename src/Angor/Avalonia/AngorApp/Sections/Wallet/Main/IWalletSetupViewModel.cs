namespace AngorApp.Sections.Wallet.Main;

public interface IWalletSetupViewModel
{
    public IEnhancedCommand Create { get; }
    public IEnhancedCommand Import { get;  }
}
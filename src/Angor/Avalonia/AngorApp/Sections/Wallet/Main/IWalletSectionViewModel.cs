namespace AngorApp.Sections.Wallet.Main;

public interface IWalletSectionViewModel : IWalletSetupViewModel
{
    public IEnumerable<IWallet> Wallets { get; }
    public IWallet CurrentWallet { get; set; }
}
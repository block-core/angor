using AngorApp.Model;
using AngorApp.Sections.Wallet.NoWallet;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class WalletProviderDesign : IWalletProvider
{
    private Maybe<IWallet> maybeWallet = Maybe<IWallet>.None;

    public Maybe<IWallet> GetWallet()
    {
        return maybeWallet;
    }

    public void SetWallet(IWallet wallet)
    {
        this.maybeWallet = wallet.AsMaybe();
    }
}
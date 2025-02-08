using Angor.UI.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Design;

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
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IWalletProvider
{
    Maybe<IWallet> GetWallet();
    void SetWallet(IWallet wallet);
}
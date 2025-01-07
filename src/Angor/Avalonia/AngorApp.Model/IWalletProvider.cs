using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.NoWallet;

public interface IWalletProvider
{
    Maybe<IWallet> GetWallet();
    void SetWallet(IWallet wallet);
}
using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IWalletProvider
{
    Maybe<IWallet> GetWallet();
    void SetWallet(IWallet wallet);
}
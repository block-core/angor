using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IWalletProvider
{
    Task<Result<IWallet>> Get(WalletId walletId);
}
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model;

public interface IWalletBuilder
{
    Task<Result<IWallet>> Get(WalletId walletId);
}
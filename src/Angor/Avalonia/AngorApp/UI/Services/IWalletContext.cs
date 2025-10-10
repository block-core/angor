using Angor.Contexts.Wallet.Domain;
using DynamicData;

namespace AngorApp.UI.Services;

public interface IWalletContext
{
    IObservable<IChangeSet<IWallet, WalletId>> WalletChanges { get; }
    IObservable<Maybe<IWallet>> CurrentWalletChanges { get; }
    Maybe<IWallet> CurrentWallet { get; set; }
    Task<Result> DeleteWallet(WalletId walletId);
}

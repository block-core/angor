using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using DynamicData;

namespace AngorApp.UI.Shared.Services;

public interface IWalletContext
{
    IObservable<IChangeSet<IWallet, WalletId>> WalletChanges { get; }
    IObservable<Maybe<IWallet>> CurrentWalletChanges { get; }
    Maybe<IWallet> CurrentWallet { get; set; }
    Task<Result> DeleteWallet(WalletId walletId);
    Task<Result<IWallet>> GetDefaultWallet();
}

using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IWalletUnlockHandler
{
    Task<Maybe<string>> RequestPassword(WalletId id);
    IObservable<WalletId> WalletUnlocked { get; }
    bool IsUnlocked(WalletId id);
    void ConfirmUnlock(WalletId id, string password);
}
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Implementation.Wallet;

public class WalletUnlockHandler : IWalletUnlockHandler
{
    public Task<Maybe<string>> RequestPassword(WalletId id)
    {
        // IMPLEMENTED IN FOLLOW-UP PR
        throw new NotImplementedException();
    }

    public IObservable<WalletId> WalletUnlocked { get; }

    public bool IsUnlocked(WalletId id)
    {
        // IMPLEMENTED IN FOLLOW-UP PR
        throw new NotImplementedException();
    }

    public void ConfirmUnlock(WalletId id, string password)
    {
        // IMPLEMENTED IN FOLLOW-UP PR
        throw new NotImplementedException();
    }
}
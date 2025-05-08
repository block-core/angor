using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public interface ISensitiveWalletDataProvider
{
    Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId);
    void SetSensitiveData(WalletId id, (string seed, Maybe<string> passphrase) data);
}
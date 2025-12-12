using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface ISensitiveWalletDataProvider
{
    Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId);
    void SetSensitiveData(WalletId id, (string seed, Maybe<string> passphrase) data);
    void RemoveSensitiveData(WalletId id);
}

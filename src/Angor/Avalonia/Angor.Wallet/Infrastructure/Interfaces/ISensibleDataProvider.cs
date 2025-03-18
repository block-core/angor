using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Interfaces;

public interface ISensitiveWalletDataProvider
{
    Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId id);
}
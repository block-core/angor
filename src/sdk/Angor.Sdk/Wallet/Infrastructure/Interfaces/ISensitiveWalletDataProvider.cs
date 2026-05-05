using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface ISensitiveWalletDataProvider
{
    Task<Result<(string seed, string? passphrase)>> RequestSensitiveData(WalletId walletId);
    void SetSensitiveData(WalletId id, (string seed, string? passphrase) data);
    void RemoveSensitiveData(WalletId id);
}

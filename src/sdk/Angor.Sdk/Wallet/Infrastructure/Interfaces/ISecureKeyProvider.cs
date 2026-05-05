using Angor.Sdk.Common;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface ISecureKeyProvider
{
    Task<string?> Get(WalletId walletId);
    Task Save(WalletId walletId, string key);
    Task Remove(WalletId walletId);
    string GenerateKey();
}

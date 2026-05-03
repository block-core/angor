using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface ISecureKeyProvider
{
    Task<Maybe<string>> Get(WalletId walletId);
    Task Save(WalletId walletId, string key);
    Task Remove(WalletId walletId);
    string GenerateKey();
}

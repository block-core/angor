using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IEncryptionKeyProvider
{
    Task<Maybe<string>> Get(WalletId walletId);
}
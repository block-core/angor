using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public interface IEncryptionKeyProvider
{
    Task<Maybe<string>> Get(WalletId walletId);
}
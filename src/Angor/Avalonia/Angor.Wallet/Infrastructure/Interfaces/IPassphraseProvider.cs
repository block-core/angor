using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IPassphraseProvider
{
    public Task<Maybe<string>> Get(WalletId walletId); 
}
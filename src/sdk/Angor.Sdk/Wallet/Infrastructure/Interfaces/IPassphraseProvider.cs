using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IPassphraseProvider
{
    public Task<Maybe<string>> Get(WalletId walletId); 
}
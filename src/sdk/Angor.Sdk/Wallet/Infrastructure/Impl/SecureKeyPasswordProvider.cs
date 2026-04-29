using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class SecureKeyPasswordProvider(ISecureKeyProvider secureKeyProvider) : IPasswordProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return secureKeyProvider.Get(walletId);
    }
}

using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class SecureKeyPasswordProvider(ISecureKeyProvider secureKeyProvider) : IPasswordProvider
{
    public Task<string?> Get(WalletId walletId)
    {
        return secureKeyProvider.Get(walletId);
    }
}

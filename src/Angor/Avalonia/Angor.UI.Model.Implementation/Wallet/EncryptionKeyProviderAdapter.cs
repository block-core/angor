using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Implementation.Wallet;

public class EncryptionKeyProviderAdapter : IEncryptionKeyProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        // IMPLEMENTED IN FOLLOW-UP PR
        throw new NotImplementedException();
    }
}
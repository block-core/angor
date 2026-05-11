using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Primitives;

namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IPassphraseProvider
{
    public Task<string?> Get(WalletId walletId);
}
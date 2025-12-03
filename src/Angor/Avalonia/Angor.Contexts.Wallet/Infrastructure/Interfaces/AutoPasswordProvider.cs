using Angor.Contexts.CrossCutting;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

/// <summary>
/// Provides auto-generated passwords for wallet encryption without user interaction.
/// </summary>
public class AutoPasswordProvider(IAutoPasswordStore autoPasswordStore) : IPasswordProvider
{
    public async Task<Maybe<string>> Get(WalletId walletId)
    {
        var password = await autoPasswordStore.GetOrCreatePasswordAsync(walletId);
        return Maybe<string>.From(password);
    }
}


using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace App.Composition.Adapters;

/// <summary>
/// Simple passphrase provider that returns no passphrase.
/// For production use, this should show a dialog to the user.
/// </summary>
public class SimplePassphraseProvider : IPassphraseProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return Task.FromResult(Maybe<string>.None);
    }
}

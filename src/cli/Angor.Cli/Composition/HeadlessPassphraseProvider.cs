using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Cli.Composition;

/// <summary>
/// Provides wallet passphrase from environment variable ANGOR_WALLET_PASSPHRASE.
/// Returns None if not set (most wallets don't use a passphrase).
/// </summary>
public class HeadlessPassphraseProvider : IPassphraseProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        var envPassphrase = Environment.GetEnvironmentVariable("ANGOR_WALLET_PASSPHRASE");
        if (!string.IsNullOrEmpty(envPassphrase))
        {
            return Task.FromResult(Maybe<string>.From(envPassphrase));
        }

        return Task.FromResult(Maybe<string>.None);
    }
}

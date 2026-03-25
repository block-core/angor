using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace App.Composition.Adapters;

/// <summary>
/// Simple password provider that returns a default encryption key.
/// For production use, this should prompt the user for their encryption key.
/// </summary>
public class SimplePasswordProvider : IPasswordProvider
{
    private string _defaultKey = "default-encryption-key";

    public void SetKey(string key) => _defaultKey = key;

    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return Task.FromResult(Maybe<string>.From(_defaultKey));
    }
}

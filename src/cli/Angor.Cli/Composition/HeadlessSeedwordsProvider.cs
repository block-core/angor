using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace Angor.Cli.Composition;

/// <summary>
/// Seedwords provider for headless mode.
/// Delegates to the SDK's built-in SeedwordsProvider which uses ISensitiveWalletDataProvider.
/// </summary>
public class HeadlessSeedwordsProvider(ISeedwordsProvider inner) : ISeedwordsProvider
{
    public Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(string walletId)
    {
        return inner.GetSensitiveData(walletId);
    }
}

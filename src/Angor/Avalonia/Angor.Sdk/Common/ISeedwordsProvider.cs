using CSharpFunctionalExtensions;

namespace Angor.Sdk.Common;

public interface ISeedwordsProvider
{
    Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(string walletId);
}
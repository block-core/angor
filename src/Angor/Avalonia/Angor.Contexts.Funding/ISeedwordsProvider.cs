using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding;

public interface ISeedwordsProvider
{
    Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(Guid walletId);
}
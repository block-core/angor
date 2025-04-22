using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Shared;

public interface ISeedwordsProvider
{
    Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(Guid walletId);
}
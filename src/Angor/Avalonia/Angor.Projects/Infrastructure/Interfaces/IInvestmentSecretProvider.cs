using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface ISensibleDataProvider
{
    Task<Result<(string seed, Maybe<string> passphrase)>> GetSecrets(Guid walletId);
}
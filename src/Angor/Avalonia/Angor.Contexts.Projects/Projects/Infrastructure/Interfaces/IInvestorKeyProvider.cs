using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Interfaces;

public interface IInvestorKeyProvider
{
    Task<Result<string>> InvestorKey(Guid walletId, string founderKey);
    Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(Guid walletId);
}
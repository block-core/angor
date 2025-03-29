using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IInvestorKeyProvider
{
    Task<Result<string>> InvestorKey(Guid walletId, string founderKey);
}
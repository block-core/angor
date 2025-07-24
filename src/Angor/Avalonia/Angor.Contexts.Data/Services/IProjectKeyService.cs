using CSharpFunctionalExtensions;

namespace Angor.Contexts.Data.Services;

public interface IProjectKeyService
{
    Task<Result<IEnumerable<(string, string)>>> GetCachedProjectKeys(Guid walletId);
    Task<Result> SaveProjectKeys(Guid walletId, IEnumerable<(string, string)> projectKeys);
    Task<Result<bool>> HasCachedProjectKeys(Guid walletId);
}
using Angor.Contexts.Data.Entities;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Data.Services;

public interface IProjectKeyService
{
    Task<Result<IEnumerable<ProjectKey>>> GetCachedProjectKeys(Guid walletId);
    Task<Result> SaveProjectKeys(Guid walletId, IEnumerable<ProjectKey> projectKeys);
    Task<Result<int>> HasProjectsKeys(Guid walletId);
}
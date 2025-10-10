using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IProjectRepository
{
    Task<Result<Project>> Get(ProjectId id);
    Task<Result<IEnumerable<Project>>> GetAll(params ProjectId[] ids);
    Task<Result<IEnumerable<Project>>> Latest();

    Task<Result<Maybe<Project>>> TryGet(ProjectId projectId);
}
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IProjectRepository
{
    Task<Result<Project>> Get(ProjectId id);
    Task<IList<Project>> Latest();

    Task<Result<Maybe<Project>>> TryGet(ProjectId projectId);
}
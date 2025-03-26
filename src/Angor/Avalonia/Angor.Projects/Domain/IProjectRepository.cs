using CSharpFunctionalExtensions;

namespace Angor.Projects.Domain;

public interface IProjectRepository
{
    Task<Result> SaveAsync(Project project);
    Task<Result<Project>> Get(ProjectId id);
    Task<IList<Project>> Latest();
}
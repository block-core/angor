using CSharpFunctionalExtensions;

namespace Angor.Projects.Domain;

public interface IProjectRepository
{
    Task<Result<Project>> Get(ProjectId id);
    Task<Result> SaveAsync(Project project);
}
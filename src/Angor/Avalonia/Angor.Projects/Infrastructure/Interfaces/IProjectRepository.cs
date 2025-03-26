using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IProjectRepository
{
    Task<Result> SaveAsync(Project project);
    Task<Result<Project>> Get(ProjectId id);
    Task<IList<Project>> Latest();
}
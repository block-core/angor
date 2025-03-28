using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IProjectRepository
{
    Task<Result<Project>> Get(ProjectId id);
    Task<Result> SaveAsync(Project project);
}
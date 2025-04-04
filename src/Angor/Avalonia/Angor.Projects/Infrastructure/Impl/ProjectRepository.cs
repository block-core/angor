using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Impl;

public class ProjectRepository : IProjectRepository
{
    public Task<Result<Project>> Get(ProjectId id)
    {
        throw new NotImplementedException();
    }

    public Task<Result> SaveAsync(Project project)
    {
        throw new NotImplementedException();
    }
}
using Angor.Contexts.Data.Entities;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Data.Services;

public interface IProjectEventService
{
    Task<Result> PullAndSaveProjectEventsAsync(params string[] eventIds);
    Task<int> SaveProjectsAsync(params Project[] projects);
    Task<IEnumerable<Project>> GetProjectsByIdsAsync(params string[] projectIds);
}
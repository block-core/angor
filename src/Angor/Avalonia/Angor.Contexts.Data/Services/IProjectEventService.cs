using Angor.Contexts.Data.Entities;

namespace Angor.Contexts.Data.Services;

public interface IProjectEventService
{
    Task<List<Project>> PullAndSaveProjectEventsAsync(params string[] eventIds);
    Task<bool> SaveProjectAsync(Project project);
    Task<List<Project>> GetAllProjectsAsync();
}
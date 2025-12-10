using CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Founder.CreateProject.Moonshot;

public interface IMoonshotService
{
    Task<Result<MoonshotProjectData>> GetMoonshotProjectAsync(string eventId);
}

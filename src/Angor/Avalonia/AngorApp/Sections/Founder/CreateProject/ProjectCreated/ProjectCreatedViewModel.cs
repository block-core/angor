using AngorApp.Core;

namespace AngorApp.Sections.Founder.CreateProject.ProjectCreated;

internal class ProjectCreatedViewModel(string transactionId, SharedCommands commands) : IProjectCreatedViewModel
{
    public IEnhancedCommand OpenTransaction { get; } = commands.OpenTransaction(transactionId);
}
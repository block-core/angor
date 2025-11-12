using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Founder;

public interface IFounderProjectViewModel
{
    ProjectId Id { get; }
    public string Name { get; }
    public string ShortDescription { get; }
    public Uri? Picture { get; }
    public Uri? Banner { get; }
    public long TargetAmount { get; }
    public IEnhancedCommand GoToDetails { get; }
}

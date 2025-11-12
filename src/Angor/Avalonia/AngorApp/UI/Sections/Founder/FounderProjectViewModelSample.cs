using System;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Founder;

public class FounderProjectViewModelSample : IFounderProjectViewModel
{
    public ProjectId Id { get; set; }
    public string Name { get; set; }
    public string ShortDescription { get; set; }
    public Uri? Picture { get; set; }
    public Uri? Banner { get; set; }
    public long TargetAmount { get; set; }
    public IEnhancedCommand GoToDetails { get; }
}

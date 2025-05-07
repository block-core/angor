using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder;

public class FounderProjectViewModelDesign : IFounderProjectViewModel
{
    public string Name { get; set; }
    public string ShortDescription { get; set; }
    public Uri? Picture { get; set; }
    public Uri? Banner { get; set; }
    public long TargetAmount { get; set; }
    public IEnhancedCommand GoToDetails { get; }
}
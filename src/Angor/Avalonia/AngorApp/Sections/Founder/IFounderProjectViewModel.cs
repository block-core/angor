using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder;

public interface IFounderProjectViewModel
{
    public string Name { get; }
    public string ShortDescription { get; }
    public Uri? Picture { get; }
    public Uri? Banner { get; }
    public long TargetAmount { get; }
    public IEnhancedCommand GoToDetails { get; }
}
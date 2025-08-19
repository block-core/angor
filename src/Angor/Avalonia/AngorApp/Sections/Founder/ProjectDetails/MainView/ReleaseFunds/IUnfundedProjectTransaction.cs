using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public interface IUnfundedProjectTransaction
{
    public DateTime Arrived { get;  }
    public DateTime Approved { get;  }
    public DateTime? Released { get; }
    IEnhancedCommand Release { get; }
}
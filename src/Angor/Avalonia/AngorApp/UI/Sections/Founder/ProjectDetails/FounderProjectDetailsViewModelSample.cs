using AngorApp.UI.Sections.Browse.Details;
using AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails;

public class FounderProjectDetailsViewModelSample : IFounderProjectDetailsViewModel
{
    public IEnhancedCommand<Result<IFullProject>> Load { get; }
    public IFullProject? Project { get; } = new FullProjectSample();
    public object? ContentViewModel { get; } = new ApproveInvestmentsViewModelSample();
}

using AngorApp.Sections.Browse.Details;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails;

public class FounderProjectDetailsViewModelSample : IFounderProjectDetailsViewModel
{
    public IEnhancedCommand<Result<IFullProject>> Load { get; }
    public IFullProject? Project { get; } = new FullProjectSample();
    public object? ContentViewModel { get; } = new ApproveInvestmentsViewModelSample();
}

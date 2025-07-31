using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public class UnfundedProjectTransactionDesign : IUnfundedProjectTransaction
{
    public DateTime Arrived { get; set; }
    public DateTime Approved { get; set; }
    public DateTime? Released { get; set; }
    public IEnhancedCommand Release { get; } = ReactiveCommand.Create(() => { }).Enhance();
}
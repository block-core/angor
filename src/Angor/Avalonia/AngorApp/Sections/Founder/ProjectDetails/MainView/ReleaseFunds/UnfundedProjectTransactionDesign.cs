using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public class UnfundedProjectTransactionDesign : IUnfundedProjectTransaction
{
    public DateTime Arrived { get; set; }
    public DateTime Approved { get; set; }
    public DateTime? Released { get; set; }
    public string InvestmentEventId { get; set; }
    public IEnhancedCommand<Maybe<Result>> Release { get; }
}
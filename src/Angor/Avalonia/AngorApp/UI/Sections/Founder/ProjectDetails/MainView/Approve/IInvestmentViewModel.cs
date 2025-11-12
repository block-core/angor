using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.Approve;

public interface IInvestmentViewModel
{
    IEnumerable<IInvestmentChild> OtherInvestments { get; }
    public IInvestmentChild MostRecentInvestment { get; }
    public IEnhancedCommand Approve { get; }
    bool AreDetailsShown { get; set; }
}
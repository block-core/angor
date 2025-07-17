using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public interface IInvestmentViewModel
{
    IEnumerable<IInvestmentChild> OtherInvestments { get; }
    public IInvestmentChild MostRecentInvestment { get; }
    public IEnhancedCommand Approve { get; }
    bool AreDetailsShown { get; set; }
}
using System.Linq;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;

public partial class InvestmentViewModelSample : ReactiveObject, IInvestmentViewModel
{
    [Reactive]
    private bool areDetailsShown;

    public IEnumerable<IInvestmentChild> OtherInvestments { get; set; } = SampleData().OrderByDescending(child => child.CreatedOn);

    private static IEnumerable<IInvestmentChild> SampleData()
    {
        return [
            new InvestmentChildViewModelSample()
            {
                Amount = new AmountUI(4000000),
                CreatedOn = DateTime.Now.AddDays(-2)
            },
            new InvestmentChildViewModelSample()
            {
                Amount = new AmountUI(3000000),
                CreatedOn = DateTime.Now.AddDays(-6)
            },

            new InvestmentChildViewModelSample()
            {
                Amount = new AmountUI(2000000),
                CreatedOn = DateTime.Now.AddDays(-1)
            },
        ];
    }

    public IInvestmentChild MostRecentInvestment { get; } = new InvestmentChildViewModelSample();

    public IEnhancedCommand Approve { get; }
}
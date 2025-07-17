using System.Linq;
using MoreLinq;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public partial class InvestmentViewModelDesign : ReactiveObject, IInvestmentViewModel
{
    [Reactive]
    private bool areDetailsShown;

    public IEnumerable<IInvestmentChild> OtherInvestments { get; set; } = SampleData().OrderByDescending(child => child.CreatedOn);

    private static IEnumerable<IInvestmentChild> SampleData()
    {
        return [
            new InvestmentChildViewModelDesign()
            {
                Amount = new AmountUI(4000000),
                CreatedOn = DateTime.Now.AddDays(-2)
            },
            new InvestmentChildViewModelDesign()
            {
                Amount = new AmountUI(3000000),
                CreatedOn = DateTime.Now.AddDays(-6)
            },

            new InvestmentChildViewModelDesign()
            {
                Amount = new AmountUI(2000000),
                CreatedOn = DateTime.Now.AddDays(-1)
            },
        ];
    }

    public IInvestmentChild MostRecentInvestment { get; } = new InvestmentChildViewModelDesign();

    public IEnhancedCommand Approve { get; }
}
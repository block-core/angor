using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Operations;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public class FounderProjectDetailsViewModelDesign : IFounderProjectDetailsViewModel
{
    public string Name { get; } = "Test";

    public IEnumerable<IInvestmentViewModel> Investments { get; set; } =
    [
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment = { Status = InvestmentStatus.Invested, }
        },
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment = { Status = InvestmentStatus.PendingFounderSignatures, }
        },
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment = { Status = InvestmentStatus.Invested, }
        },
        new InvestmentViewModelDesign()
        {
            MostRecentInvestment =
            {
                Status = InvestmentStatus.Invalid,
            },
            OtherInvestments = [],
        },
    ];

    public ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
    public Uri? BannerUrl { get; set; }
    public string ShortDescription { get; } = "Short description, Bitcoin ONLY.";
    public IEnhancedCommand GoManageFunds { get; }
}
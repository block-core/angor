using Angor.Sdk.Funding.Investor;
using AngorApp.UI.Sections.Portfolio;
using AngorApp.UI.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;

public class FoundedProjectOptionsViewModelSample : IFoundedProjectOptionsViewModel
{
    public IEnhancedCommand CompleteInvestment { get; set; }
    public IObservable<IPortfolioProjectViewModel> ProjectInvestment { get; set; }
    public ReactiveCommand<Unit, Result<Maybe<InvestedProjectDto>>> LoadInvestment { get; set; }
}
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Controls.Common.FoundedProjectOptions;

public class FoundedProjectOptionsViewModelDesign : IFoundedProjectOptionsViewModel
{
    public IEnhancedCommand CompleteInvestment { get; set; }
    public IObservable<IPortfolioProjectViewModel> ProjectInvestment { get; set; }
    public ReactiveCommand<Unit, Result<Maybe<InvestedProjectDto>>> LoadInvestment { get; set; }
}
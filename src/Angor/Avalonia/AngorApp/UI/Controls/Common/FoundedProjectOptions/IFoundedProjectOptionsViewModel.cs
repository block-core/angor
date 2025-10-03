using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Controls.Common.FoundedProjectOptions;

public interface IFoundedProjectOptionsViewModel
{
    IObservable<IPortfolioProjectViewModel> ProjectInvestment { get; }
    ReactiveCommand<Unit, Result<Maybe<InvestedProjectDto>>> LoadInvestment { get; set; }
}
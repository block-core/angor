using Angor.Sdk.Funding.Investor;
using AngorApp.UI.Sections.Portfolio;
using AngorApp.UI.Sections.Portfolio.Items;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;

public interface IFoundedProjectOptionsViewModel
{
    IObservable<IPortfolioProjectViewModel> ProjectInvestment { get; }
    ReactiveCommand<Unit, Result<Maybe<InvestedProjectDto>>> LoadInvestment { get; set; }
}
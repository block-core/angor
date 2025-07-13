using Angor.Contexts.Funding.Founder.Operations;
using Zafiro.UI;

namespace AngorApp.Sections.Founder.Details;

public interface IFounderProjectDetailsViewModel
{
    public string Name { get; }
    public IEnumerable<IdentityContainer<IInvestmentViewModel>> Investments { get; }
    ReactiveCommand<Unit, Result<IEnumerable<IInvestmentViewModel>>> LoadInvestments { get; }
    public Uri? BannerUrl { get; }
    public string ShortDescription { get; }
}
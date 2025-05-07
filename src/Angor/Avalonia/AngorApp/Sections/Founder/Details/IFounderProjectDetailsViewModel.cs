using Angor.Contexts.Funding.Founder.Operations;

namespace AngorApp.Sections.Founder.Details;

public interface IFounderProjectDetailsViewModel
{
    public string Name { get; }
    public IEnumerable<GetPendingInvestments.PendingInvestmentDto> PendingInvestments { get; }
    ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> LoadPendingInvestments { get; }
    public Uri? BannerUrl { get; }
    public string ShortDescription { get; }
}
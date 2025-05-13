using Angor.Contexts.Funding.Founder.Operations;

namespace AngorApp.Sections.Founder.Details;

public class FounderProjectDetailsViewModelDesign : IFounderProjectDetailsViewModel
{
    public string Name { get; } = "Test";

    public IEnumerable<GetPendingInvestments.PendingInvestmentDto> PendingInvestments { get; } = new List<GetPendingInvestments.PendingInvestmentDto>()
    {
        new(DateTime.Now, 1234, "nostr pub key"),
        new(DateTime.Now.AddHours(-2), 1233, "nostr pub key"),
        new(DateTime.Now.AddHours(-4), 1235, "nostr pub key"),
    };

    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> LoadPendingInvestments { get; }
    public Uri? BannerUrl { get; set; }
    public string ShortDescription { get; } = "Short description, Bitcoin ONLY.";
}
using Angor.Contexts.Funding.Investor;

namespace AngorApp.Sections.Portfolio;

public class InvestedProject(InvestedProjectDto projectDto) : IInvestedProject
{
    public string Name => projectDto.Name;
    public string Description => projectDto.Description;
    public IAmountUI Target => new AmountUI(projectDto.Target.Sats);
    public IAmountUI Raised => new AmountUI(projectDto.Raised.Sats);
    public IAmountUI InRecovery => new AmountUI(projectDto.InRecovery.Sats);
    public ProjectStatus Status => ProjectStatus.Funding;
    public FounderStatus FounderStatus => FounderStatus.Approved;
    public Uri LogoUri => projectDto.LogoUri;
}
using Angor.Contexts.Funding.Investor;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    IReadOnlyCollection<PortfolioItem> Items { get; }
    public IEnumerable<IPortfolioProject> InvestedProjects { get; }
    public IEnhancedCommand<Result<IEnumerable<InvestedProjectDto>>> Load { get; }
}

public interface IPortfolioProject
{
    public string Name { get; }
    public string Description { get; }
    public IAmountUI Target { get; }
    public IAmountUI Raised { get; }
    public IAmountUI InRecovery { get; }
    public ProjectStatus Status { get; }
    public FounderStatus FounderStatus { get; }
    public Uri LogoUri { get; }
    public double Progress => Target.Sats == 0 ? 0 : Raised.Sats / (double)Target.Sats;
    public IEnhancedCommand<Result> CompleteInvestment { get; }
}

public class Property
{
    public string Name { get; set; }
    public object Value { get; set; }
}

public enum FounderStatus
{
    Invalid,
    Approved
}

public enum ProjectStatus
{
    Invalid,
    Funding,
    Finished,
    Cancelled,
}
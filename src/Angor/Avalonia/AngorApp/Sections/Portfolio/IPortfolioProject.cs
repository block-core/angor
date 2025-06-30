using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public interface IPortfolioProject
{
    public string Name { get; }
    public string Description { get; }
    public IAmountUI Target { get; }
    public IAmountUI Raised { get; }
    public IAmountUI InRecovery { get; }
    public InvestmentStatus InvestmentStatus { get; }
    public FounderStatus FounderStatus { get; }
    public Uri LogoUri { get; }
    public double Progress => Target.Sats == 0 ? 0 : Raised.Sats / (double)Target.Sats;
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    public bool IsInvestmentCompleted { get; }
    public IAmountUI Invested { get; }
}
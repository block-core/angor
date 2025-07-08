using Angor.Contexts.Funding.Projects.Infrastructure.Impl;

namespace Angor.Contexts.Funding.Investor.Operations;

public class InvestmentRecords
{
    public List<InvestorPositionRecord> ProjectIdentifiers { get; set; } = new();
}
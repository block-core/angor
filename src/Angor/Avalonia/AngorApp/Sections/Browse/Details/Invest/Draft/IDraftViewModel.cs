using Angor.Contexts.Funding.Investor.CreateInvestment;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public interface IDraftViewModel
{
    public long SatsToInvest { get; }
    ReactiveCommand<Unit, Result<InvestmentTransaction>> CreateDraft { get; set; }
}
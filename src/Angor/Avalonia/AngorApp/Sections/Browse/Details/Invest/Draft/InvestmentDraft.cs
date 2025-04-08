using Angor.Contexts.Funding.Investor;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public class InvestmentDraft(Angor.Contexts.Funding.Investor.CreateInvestment.Draft transaction)
{
    public long TotalFee => transaction.TotalFee.Sats;
}
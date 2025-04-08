using Angor.Contexts.Funding.Investor.CreateInvestment;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public class InvestmentDraft(InvestmentTransaction transaction)
{
    public long TotalFee => transaction.TotalFee.Sats;
}
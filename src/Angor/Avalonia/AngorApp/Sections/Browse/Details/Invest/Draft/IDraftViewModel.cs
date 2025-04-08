namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public interface IDraftViewModel
{
    public long SatsToInvest { get; }
    InvestmentDraft Draft { get; }
    public long Feerate { get; set; }
}
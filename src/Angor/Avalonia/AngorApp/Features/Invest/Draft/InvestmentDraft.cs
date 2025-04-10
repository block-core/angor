using Angor.Contexts.Funding.Investor;

namespace AngorApp.Features.Invest.Draft;

public class InvestmentDraft(Angor.Contexts.Funding.Investor.CreateInvestment.Draft draftModel)
{
    public CreateInvestment.Draft DraftModel { get; } = draftModel;

    public long TotalFee => DraftModel.TotalFee.Sats;
}
using Angor.Contexts.Funding.Investor.Operations;

namespace AngorApp.Features.Invest.Draft;

public class InvestmentDraft(CreateInvestment.Draft draftModel)
{
    public CreateInvestment.Draft DraftModel { get; } = draftModel;

    public long TotalFee => DraftModel.TotalFee.Sats;
}
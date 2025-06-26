using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;

namespace AngorApp.Features.Invest.Draft;

public class InvestmentDraft(IInvestmentAppService investmentAppService, IWallet wallet, IProject project,  CreateInvestment.Draft draftModel) : IInvestmentDraft
{
    public CreateInvestment.Draft DraftModel { get; } = draftModel;

    public AmountUI TotalFee => new AmountUI(DraftModel.TotalFee.Sats);

    public async Task<Result> Confirm()
    {
        return await investmentAppService.Invest(wallet.Id.Value, new ProjectId(project.Id), DraftModel);
    }
}
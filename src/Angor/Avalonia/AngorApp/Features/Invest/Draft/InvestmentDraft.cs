using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;

namespace AngorApp.Features.Invest.Draft;

public class InvestmentDraft(IInvestmentAppService investmentAppService, IWallet wallet, IProject project,  CreateInvestment.Draft draftModel) : IInvestmentDraft
{
    public CreateInvestment.Draft DraftModel { get; } = draftModel;

    public IAmountUI TransactionFee => new AmountUI(DraftModel.TransactionFee.Sats);
    public IAmountUI MinerFee => new AmountUI(DraftModel.MinerFee.Sats);
    public IAmountUI AngorFee => new AmountUI(DraftModel.AngorFee.Sats);

    public Task<Result<Guid>> Confirm()
    {
        return investmentAppService.Invest(wallet.Id.Value, new ProjectId(project.Id), DraftModel);
    }
}
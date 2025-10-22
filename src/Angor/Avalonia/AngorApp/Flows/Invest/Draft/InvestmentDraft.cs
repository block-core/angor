using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.UI.Model.Implementation.Projects;

namespace AngorApp.Flows.Invest.Draft;

using Draft = Angor.Contexts.Funding.Shared.TransactionDrafts.InvestmentDraft;

public class InvestmentDraft(IInvestmentAppService investmentAppService, IWallet wallet, FullProject project, Draft draftModel) : IInvestmentDraft
{
    public Draft DraftModel { get; } = draftModel;

    public IAmountUI TransactionFee => new AmountUI(DraftModel.TransactionFee.Sats);
    public IAmountUI MinerFee => new AmountUI(DraftModel.MinerFee.Sats);
    public IAmountUI AngorFee => new AmountUI(DraftModel.AngorFee.Sats);

    public async Task<Result<Guid>> Confirm()
    {
        if (draftModel.PenaltyDisabled)
        {
             var res = await investmentAppService.SubmitTransactionFromDraft(wallet.Id.Value, project.Info.Id, DraftModel);
             return res.IsSuccess ? Result.Success<Guid>(Guid.Empty) : Result.Failure<Guid>(res.Error);
        }

        return await investmentAppService.SubmitInvestment(wallet.Id.Value, project.Info.Id, DraftModel);
    }
}

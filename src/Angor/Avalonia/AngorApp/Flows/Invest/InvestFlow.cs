using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Contexts.Wallet.Domain;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.TransactionDrafts;
using AngorApp.TransactionDrafts.DraftTypes;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using AmountViewModel = AngorApp.Flows.Invest.Amount.AmountViewModel;
using ITransactionDraftViewModel = AngorApp.TransactionDrafts.DraftTypes.ITransactionDraftViewModel;

namespace AngorApp.Flows.Invest;

public class InvestFlow(IInvestmentAppService investmentAppService, UIServices uiServices)
{
    public Task<Maybe<Unit>> Invest(IWallet wallet, FullProject fullProject)
    {
        var p = WizardBuilder
            .StartWith(() => new AmountViewModel(wallet, fullProject), "Enter the amount to invest").NextWith(model => model.Amount)
            .Then(amount => CreateDraft(wallet.Id, fullProject.ProjectId, amount!.Value), "Transaction Preview").NextWith(model => model.CommitDraft.Enhance("Invest"))
            .Then(_ => new SuccessViewModel(SuccessMessage()), "Investment Successful").NextAlways("Close")
            .WithCompletionFinalStep();

        return uiServices.Dialog.ShowWizard(p, @$"Invest in ""{fullProject.Info.Name}""");
    }

    private string SuccessMessage()
    {
        return "The investment offer has been sent. The project founder will review your investment offer soon and either accept it or reject it";
    }

    private TransactionDraftPreviewerViewModel CreateDraft(WalletId walletId, ProjectId projectId, long satsToInvest)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(feerate =>
        {
            var amount = new Angor.Contexts.Funding.Projects.Domain.Amount(satsToInvest);
            var investmentDraft = investmentAppService.CreateInvestmentDraft(walletId.Value, projectId, amount, new DomainFeerate(feerate));
            var viewModel = investmentDraft.Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel(draft, uiServices));
            return viewModel;
        }, draft => investmentAppService.SubmitInvestment(walletId.Value, projectId, (InvestmentDraft)draft.Model), uiServices)
        {
            Amount = new AmountUI(satsToInvest)
        };
        return transactionDraftPreviewerViewModel;
    }
}
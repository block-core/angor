using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Model.Projects;
using AngorApp.TransactionDrafts;
using AngorApp.TransactionDrafts.DraftTypes;
using AngorApp.TransactionDrafts.DraftTypes.Investment;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Wizards.Slim.Builder;
using AmountViewModel = AngorApp.Flows.Invest.Amount.AmountViewModel;
using ITransactionDraftViewModel = AngorApp.TransactionDrafts.DraftTypes.ITransactionDraftViewModel;

namespace AngorApp.Flows.Invest;

public class InvestFlow(IInvestmentAppService investmentAppService, UIServices uiServices)
{
    public async Task<Maybe<Unit>> Invest(IWallet wallet, FullProject fullProject)
    {
        var wizard = WizardBuilder
            .StartWith(() => new AmountViewModel(wallet, fullProject), "Enter the amount to invest").NextCommand(model => ReactiveCommand.CreateFromTask(() => IsAboveThreshold(fullProject.ProjectId, model.Amount!.Value).Map(above => new{ above, amount = model.Amount.Value })).Enhance("Next"))
            .Then(investData => CreateDraft(wallet.Id, fullProject, investData.amount, investData.above), "Transaction Preview").NextCommand((model, investData) => InvestCommand(model.CommitDraft, investData.above).Enhance(investData.above ? "Submit Offer" : "Invest Now"))
            .Then(message => new SuccessViewModel(message), "Investment Successful").Next(_ => Unit.Default, "Close").Always()
            .WithCompletionFinalStep();

        return await uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{fullProject.Name}""");
    }

    private Task<Result<bool>> IsAboveThreshold(ProjectId projectId, long investmentAmount)
    {
        return investmentAppService.IsInvestmentAbovePenaltyThreshold(projectId, new Angor.Contexts.Funding.Projects.Domain.Amount(investmentAmount));
    }

    private static IEnhancedCommand<Result<string>> InvestCommand(IEnhancedCommand<Result<Guid>> command, bool isAboveThreshold)
    {
        return ReactiveCommand.CreateFromObservable(() =>
        {
            return command.Execute().Take(1).Map(txId => SuccessMessage(isAboveThreshold));
        }, canExecute: ((IReactiveCommand)command).CanExecute)
        .Enhance();
    }

    private TransactionDraftPreviewerViewModel CreateDraft(WalletId walletId, FullProject fullProject, long satsToInvest, bool isAboveThreshold)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
            feerate =>
            {
                var amount = new Angor.Contexts.Funding.Projects.Domain.Amount(satsToInvest);
                var investmentDraft = investmentAppService.CreateInvestmentDraft(walletId.Value, fullProject.ProjectId, amount, new DomainFeerate(feerate));
                var viewModel = investmentDraft.Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel(draft, uiServices));
                return viewModel;
            }, 
            async draft =>
            {
                // Get the actual draft model from the view model
                var investmentDraft = (InvestmentDraft)draft.Model;
                
                if (!isAboveThreshold)
                {
                    // Below threshold: directly publish the transaction and store in portfolio (no founder approval needed)
                    var publishResult = await investmentAppService.SubmitTransactionFromDraft(walletId.Value, fullProject.ProjectId, investmentDraft);
                    
                    // Return a GUID representing the transaction (use a hash or placeholder)
                    return publishResult.IsSuccess 
                        ? Result.Success(Guid.NewGuid()) // Transaction was published directly
                        : Result.Failure<Guid>(publishResult.Error);
                }
                else
                {
                    // Above/at threshold: request founder signatures (penalty path)
                    return await investmentAppService.SubmitInvestment(walletId.Value, fullProject.ProjectId, investmentDraft);
                }
            }, 
            uiServices)
        {
            Amount = new AmountUI(satsToInvest)
        };
        
        return transactionDraftPreviewerViewModel;
    }
    
    private static string SuccessMessage(bool isAboveThreshold)
    {
        if (!isAboveThreshold)
        {
            return "Your investment has been successfully broadcast to the network. You can now recover your funds immediately without any penalty period.";
        }

        return "The investment offer has been sent. The project founder will review your investment offer soon and either accept it or reject it. Note: This investment will be subject to a penalty period if you choose to recover funds early.";
    }
}
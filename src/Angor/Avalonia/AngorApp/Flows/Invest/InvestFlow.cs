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
        // Store the investment amount to check threshold later
        long investmentAmount = 0;
        
        var wizard = WizardBuilder
            .StartWith(() => new AmountViewModel(wallet, fullProject), "Enter the amount to invest")
                .Next(model => 
                {
                    investmentAmount = model.Amount ?? 0;
                    return model.Amount;
                }).Always()
            .Then(amount => CreateDraft(wallet.Id, fullProject, amount!.Value), "Transaction Preview")
                .NextCommand(model => model.CommitDraft.Enhance(GetSubmitButtonText(fullProject.ProjectId, investmentAmount)))
            .Then(investmentId => HandleInvestmentResult(fullProject.ProjectId, investmentAmount, investmentId), "Investment Successful")
                .Next(_ => Unit.Default, "Close").Always()
            .WithCompletionFinalStep();

        return uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{fullProject.Name}""");
    }

    private string GetSubmitButtonText(ProjectId projectId, long investmentAmount)
    {
        // Use the centralized threshold check from the service
        var isAboveThresholdResult = investmentAppService.IsInvestmentAbovePenaltyThreshold(
            projectId, 
            new Angor.Contexts.Funding.Projects.Domain.Amount(investmentAmount)).Result;
        
        var isAboveThreshold = isAboveThresholdResult.IsSuccess && isAboveThresholdResult.Value;
        
        return isAboveThreshold ? "Request Approval" : "Invest Now";
    }

    private async Task<Result<SuccessViewModel>> HandleInvestmentResult(ProjectId projectId, long investmentAmount, Guid investmentIdOrTxId)
    {
        // Use the centralized threshold check from the service
        var isAboveThresholdResult = await investmentAppService.IsInvestmentAbovePenaltyThreshold(
            projectId, 
            new Angor.Contexts.Funding.Projects.Domain.Amount(investmentAmount));

        if (isAboveThresholdResult.IsFailure)
        {
            return Result.Failure<SuccessViewModel>(isAboveThresholdResult.Error);
        }

        var isAboveThreshold = isAboveThresholdResult.Value;

        string message;
        if (!isAboveThreshold)
        {
            // Below threshold
            message = "Your investment has been successfully broadcast to the network. You can now recover your funds immediately without any penalty period.";
        }
        else
        {
            // Above threshold
            message = "The investment offer has been sent. The project founder will review your investment offer soon and either accept it or reject it. Note: This investment will be subject to a penalty period if you choose to recover funds early.";
        }

        return Result.Success(new SuccessViewModel(message));
    }

    private TransactionDraftPreviewerViewModel CreateDraft(WalletId walletId, FullProject fullProject, long satsToInvest)
    {
        // Use the centralized threshold check from the service
        var isAboveThresholdResult = investmentAppService.IsInvestmentAbovePenaltyThreshold(
            fullProject.ProjectId, 
            new Angor.Contexts.Funding.Projects.Domain.Amount(satsToInvest)).Result;
        
        var isAboveThreshold = isAboveThresholdResult.IsSuccess && isAboveThresholdResult.Value;

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
                    // Below threshold: directly publish the transaction (no founder approval needed)
                    var publishResult = await investmentAppService.SubmitTransactionFromDraft(walletId.Value, investmentDraft);
                    
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
}
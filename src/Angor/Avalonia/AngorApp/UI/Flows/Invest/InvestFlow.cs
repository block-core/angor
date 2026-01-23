using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using AngorApp.Model.Projects;
using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.TransactionDrafts.DraftTypes.Investment;
using AngorApp.UI.Shared.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using AmountViewModel = AngorApp.UI.Flows.Invest.Amount.AmountViewModel;
using ITransactionDraftViewModel = AngorApp.UI.TransactionDrafts.DraftTypes.ITransactionDraftViewModel;

namespace AngorApp.UI.Flows.Invest;

public class InvestFlow(IInvestmentAppService investmentAppService, UIServices uiServices)
{
    public async Task<Maybe<Unit>> Invest(IWallet wallet, FullProject fullProject)
    {
        var wizard = WizardBuilder
           .StartWith(() => new AmountViewModel(wallet, fullProject), "Enter the amount to invest")
                  .NextCommand(model => ReactiveCommand.CreateFromTask(() =>
                  IsAboveThreshold(fullProject.ProjectId, model.Amount!.Value)
                  .Map(above => new { above, amount = model.Amount.Value, patternIndex = model.SelectedPatternIndex }))
                  .Enhance("Next"))
                  .Then(investData => CreateDraft(wallet.Id, fullProject, investData.amount, investData.above, investData.patternIndex), "Transaction Preview")
                  .NextCommand((model, investData) => InvestCommand(model.CommitDraft, investData.above).Enhance(investData.above ? "Submit Offer" : "Invest Now"))
                  .Then(message => new SuccessViewModel(message), "Investment Successful")
                  .Next(_ => Unit.Default, "Close").Always()
                  .Build(StepKind.Completion);

        return await uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{fullProject.Name}""");
    }

    private Task<Result<bool>> IsAboveThreshold(ProjectId projectId, long investmentAmount)
    {
        return investmentAppService.IsInvestmentAbovePenaltyThreshold(new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, new Angor.Sdk.Funding.Projects.Domain.Amount(investmentAmount)))
         .Map(response => response.IsAboveThreshold);
    }

    private static IEnhancedCommand<Result<string>> InvestCommand(IEnhancedCommand<Result<Guid>> command, bool isAboveThreshold)
    {
        return ReactiveCommand.CreateFromObservable(() =>
   {
       return command.Execute().Take(1).Map(txId => SuccessMessage(isAboveThreshold));
   },
  canExecute: ((IReactiveCommand)command).CanExecute).Enhance();
    }

    private TransactionDraftPreviewerViewModel CreateDraft(
            WalletId walletId,
            FullProject fullProject,
            long satsToInvest,
            bool isAboveThreshold,
            byte? patternIndex)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
              feerate =>
           {
               var amount = new Angor.Sdk.Funding.Projects.Domain.Amount(satsToInvest);

               // Pass pattern index and investment start date for Fund/Subscribe projects
               var investmentDraft = investmentAppService.BuildInvestmentDraft(
                new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                    walletId,
                    fullProject.ProjectId,
                    amount,
                    new DomainFeerate(feerate),
                    patternIndex,
                    DateTime.UtcNow)); // Investment start date defaults to now

               var viewModel = investmentDraft.Map(ITransactionDraftViewModel (response) => new InvestmentTransactionDraftViewModel(response.InvestmentDraft, uiServices));

               return viewModel;
           },
            async draft =>
           {
               // Get the actual draft model from the view model
               var investmentDraft = (InvestmentDraft)draft.Model;

               if (!isAboveThreshold)
               {
                   // Below threshold: directly publish the transaction and store in portfolio (no founder approval needed)
                   var publishResult = await investmentAppService.SubmitTransactionFromDraft(
                    new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(walletId.Value, fullProject.ProjectId, investmentDraft));

                   // Return a GUID representing the transaction (use a hash or placeholder)
                   return publishResult.IsSuccess // todo: change this pointless guid
                       ? Result.Success(Guid.NewGuid()) // Transaction was published directly
                     : Result.Failure<Guid>(publishResult.Error);
               }
               else
               {
                   // Above/at threshold: request founder signatures (penalty path)
                   var result = await investmentAppService.SubmitInvestment(new RequestInvestmentSignatures.RequestFounderSignaturesRequest(walletId, fullProject.ProjectId, investmentDraft));
                   return result.Map(response => response.InvestmentId);
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
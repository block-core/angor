using Angor.Contexts.Funding.Investor;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.UI.Controls.Common.Success;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using AmountViewModel = AngorApp.Flows.Invest.Amount.AmountViewModel;
using DraftViewModel = AngorApp.Flows.Invest.Draft.DraftViewModel;

namespace AngorApp.Flows.Invest;

public class InvestFlow(IInvestmentAppService investmentAppService, UIServices uiServices)
{
    public Task<Maybe<Unit>> Invest(IWallet wallet, FullProject fullProject)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(wallet, fullProject), model => ReactiveCommand.Create(() => Result.Success(model.Amount), model.IsValid).Enhance("Next"), $"Enter the amount you want")
            .Then(amount => new DraftViewModel(investmentAppService, wallet, new AmountUI(amount!.Value), fullProject, uiServices), model => model.Confirm.Enhance("Place Offer"), "Offer investment")
            .Then(_ => new SuccessViewModel($"The investment offer to {fullProject.Info.Name} has been sent. The project founder will review your investment offer soon and either accept it or reject it"), _ => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Close"), "Investment Successful")
            .WithCompletionFinalStep();
        
        return uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{fullProject.Info.Name}""");
    }
}
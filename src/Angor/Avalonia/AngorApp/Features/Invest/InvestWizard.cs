using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs.Wizards.Slim;
using Zafiro.UI.Commands;
using Zafiro.UI.Wizards.Slim.Builder;
using AmountViewModel = AngorApp.Features.Invest.Amount.AmountViewModel;
using DraftViewModel = AngorApp.Features.Invest.Draft.DraftViewModel;

namespace AngorApp.Features.Invest;

public class InvestWizard(IInvestmentAppService investmentAppService, UIServices uiServices)
{
    public Task<Maybe<Unit>> Invest(IWallet wallet, IProject project)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(wallet, project), model => ReactiveCommand.Create(() => Result.Success(model.Amount), model.IsValid).Enhance("Next"), $"Invest in {project.Name}")
            .Then(amount => new DraftViewModel(investmentAppService, wallet, amount!.Value, project, uiServices), model => model.Confirm.Enhance("Offer Investment"), "Amount")
            .Then(_ => new SuccessViewModel($"The investment offer to {project.Name} has been sent. The project founder will review your investment offer soon and either accept it or reject it"), _ => ReactiveCommand.Create(() => Result.Success(Unit.Default)).Enhance("Close"), "Investment Successful")
            .WithCompletionFinalStep();
        
        return uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{project}""");
    }
}
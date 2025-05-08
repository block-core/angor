using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using AngorApp.Features.Invest.Commit;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using AmountViewModel = AngorApp.Features.Invest.Amount.AmountViewModel;
using DraftViewModel = AngorApp.Features.Invest.Draft.DraftViewModel;

namespace AngorApp.Features.Invest;

public class InvestWizard(IInvestmentAppService investmentAppService, UIServices uiServices)
{
    public Task<Maybe<Unit>> Invest(IWallet wallet, IProject project)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(wallet, project))
            .Then(amountViewModel => new DraftViewModel(investmentAppService, wallet, amountViewModel.Amount!.Value, project, uiServices))
            .Then(draftViewModel => new CommitViewModel(investmentAppService, uiServices, wallet, draftViewModel.SatsToInvest, project, draftViewModel.Draft!.DraftModel))
            .Then(_ => new SuccessViewModel($"Invested in {project.Name}", "Success"))
            .FinishWith(_ => Unit.Default);

        return uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{project}""");
    }
}
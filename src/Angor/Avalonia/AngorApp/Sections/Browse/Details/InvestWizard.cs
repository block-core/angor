using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Browse.Details.Invest.Amount;
using AngorApp.Sections.Browse.Details.Invest.Draft;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Browse.Details;

public class InvestWizard(IInvestmentAppService investmentAppService, IWalletAppService walletAppService, UIServices uiServices)
{
    public Task<Maybe<Unit>> Invest(WalletId walletId, IProject project)
    {
        var wizard = WizardBuilder.StartWith(() => new AmountViewModel(walletId, walletAppService, project))
            .Then(amount => new DraftViewModel(investmentAppService, walletId, amount.Amount!.Value, project))
            .Then(_ => new SuccessViewModel($"Invested in {project.Name}", "Success"))
            .FinishWith(_ => Unit.Default);

        return uiServices.Dialog.ShowWizard(wizard, @$"Invest in ""{project}""");
    }
}
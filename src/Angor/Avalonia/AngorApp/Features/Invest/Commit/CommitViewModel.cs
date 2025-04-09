using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Wallet.Domain;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Features.Invest.Commit;

public class CommitViewModel : ReactiveObject, IStep, ICommitViewModel
{
    public CommitViewModel(IInvestmentAppService investmentAppService, UI.Services.UIServices uiServices, IWallet walletId, IProject project, CreateInvestment.Draft draft)
    {
        RequestInvestment = ReactiveCommand.CreateFromTask(() => investmentAppService.RequestInvestment(walletId.Id.Value, new ProjectId(project.Id), draft));
        RequestInvestment.HandleErrorsWith(uiServices.NotificationService, "Investment request failed");
    }

    public ReactiveCommand<Unit,Result<Guid>> RequestInvestment { get;  }
    public IObservable<bool> IsValid => RequestInvestment.Successes().Any(); 
    public IObservable<bool> IsBusy => RequestInvestment.IsExecuting;
    public bool AutoAdvance { get; } = true;
}
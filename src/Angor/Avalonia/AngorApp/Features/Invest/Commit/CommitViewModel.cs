using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Features.Invest.Commit;

public class CommitViewModel : ReactiveObject, IStep, ICommitViewModel
{
    public CommitViewModel(IInvestmentAppService investmentAppService, UIServices uiServices, IWallet wallet, long satsToInvest, IProject project, CreateInvestment.Draft draft)
    {
        RequestInvestment = ReactiveCommand.CreateFromTask(() => investmentAppService.RequestInvestment(wallet.Id.Value, new ProjectId(project.Id), draft));
        RequestInvestment.HandleErrorsWith(uiServices.NotificationService, "Investment request failed");
        Totalfee = draft.TotalFee.Sats;
        SatsToInvest = satsToInvest;
        IsInvesting = RequestInvestment.IsExecuting;
    }

    public ReactiveCommand<Unit,Result<Guid>> RequestInvestment { get;  }
    public long SatsToInvest { get; }
    public long Totalfee { get; }
    public IObservable<bool> IsInvesting { get; }
    public IObservable<bool> IsValid => RequestInvestment.Successes().Any(); 
    public IObservable<bool> IsBusy => RequestInvestment.IsExecuting;
    public bool AutoAdvance { get; } = true;
    public Maybe<string> Title => "Confirm your investment";
}
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IStep, IDraftViewModel
{
    public DraftViewModel(IInvestmentAppService investmentAppService, WalletId walletId, long sats, IProject project)
    {
        Feerate = 100;
        SatsToInvest = sats;
        ReactiveCommand.CreateFromTask(() => investmentAppService.CreateInvestmentTransaction(walletId.Id, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats)));

        var drafts = this.WhenAnyValue(x => x.Feerate)
            .Select(fr => Observable.FromAsync(() => investmentAppService.CreateInvestmentTransaction(walletId.Id, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats))))
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .Switch()
            .Successes()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(transaction => new InvestmentDraft(transaction));
        
        draftHelper = drafts.ToProperty(this, x => x.Draft);
        IsValid = this.WhenAnyValue(x => x.Draft).NotNull();
        IsValid.Subscribe(b => { });
    }

    [ObservableAsProperty] private InvestmentDraft? draft;

    public IObservable<bool> IsValid { get; }
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public long SatsToInvest { get; }

    [Reactive] private long feerate;
}
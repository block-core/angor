using System.Reactive.Subjects;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Browse.Details.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IDraftViewModel
{
    private readonly BehaviorSubject<bool> isBusy = new(true);
    
    public DraftViewModel(IInvestmentAppService investmentAppService, WalletId walletId, long sats, IProject project)
    {
        Feerate = 100;
        SatsToInvest = sats;
        ReactiveCommand.CreateFromTask(() => investmentAppService.CreateDraft(walletId.Id, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats)));

        var drafts = this.WhenAnyValue(x => x.Feerate)
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => isBusy.OnNext(true))
            .Select(fr => Observable.FromAsync(() => investmentAppService.CreateDraft(walletId.Id, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats))))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => isBusy.OnNext(false), _ => isBusy.OnNext(false))
            .Successes()
            .Select(transaction => new InvestmentDraft(transaction));
        
        draftHelper = drafts.ToProperty(this, x => x.Draft);
        IsValid = this.WhenAnyValue(x => x.Draft).NotNull();
    }

    [ObservableAsProperty] private InvestmentDraft? draft;

    public IObservable<bool> IsValid { get; }
    public IObservable<bool> IsBusy => isBusy.AsObservable();
    public bool AutoAdvance => false;
    public long SatsToInvest { get; }

    [Reactive] private long feerate;
}
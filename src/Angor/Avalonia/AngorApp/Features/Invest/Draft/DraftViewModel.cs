using System.Reactive.Subjects;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Controls;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Features.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IDraftViewModel
{
    private readonly UIServices uiServices;
    [ObservableAsProperty] private InvestmentDraft? draft;
    [Reactive] private long? feerate;
    private readonly BehaviorSubject<bool> isBusy = new(false);

    public DraftViewModel(IInvestmentAppService investmentAppService, IWallet walletId, long sats, IProject project, UIServices uiServices)
    {
        this.uiServices = uiServices;
        SatsToInvest = sats;

        var drafts = this.WhenAnyValue(x => x.Feerate)
            .WhereNotNull()
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => isBusy.OnNext(true))
            .Select(_ => Observable.FromAsync(() => investmentAppService.CreateInvestmentDraft(walletId.Id.Value, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats))))
            .Switch()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => isBusy.OnNext(false), _ => isBusy.OnNext(false))
            .Successes()
            .Select(transaction => new InvestmentDraft(transaction));

        draftHelper = drafts.ToProperty(this, x => x.Draft);
        IsValid = this.WhenAnyValue(x => x.Draft).CombineLatest(IsBusy, (investmentDraft, busy) => investmentDraft != null && !busy);

        FeeCalculator = new FeeCalculatorDesignTime();
    }

    public IObservable<bool> IsValid { get; }
    public IObservable<bool> IsBusy => isBusy.AsObservable();
    public bool AutoAdvance => false;
    public long SatsToInvest { get; }
    public IFeeCalculator FeeCalculator { get; }
    public IEnumerable<IFeeratePreset> Presets => uiServices.FeeratePresets;
}
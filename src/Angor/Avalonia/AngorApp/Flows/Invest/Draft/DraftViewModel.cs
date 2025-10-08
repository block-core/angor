using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.UI.Controls.Feerate;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Reactive;

namespace AngorApp.Flows.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IDraftViewModel, IDisposable
{
    public FullProject Project { get; }
    public IAmountUI AmountToOffer { get; }
    private readonly UIServices uiServices;
    [ObservableAsProperty] private IInvestmentDraft? draft;
    [Reactive] private long? feerate;
    [ObservableAsProperty] private IAmountUI? fee;
    private readonly BehaviorSubject<bool> isCalculatingDraft = new(false);
    private readonly CompositeDisposable disposable = new();

    public DraftViewModel(IInvestmentAppService investmentAppService, IWallet wallet, IAmountUI amountToOffer, FullProject project, UIServices uiServices)
    {
        AmountToOffer = amountToOffer;
        Project = project;
        this.uiServices = uiServices;

        isCalculatingDraft.DisposeWith(disposable);

        var createDraft = this.WhenAnyValue(x => x.Feerate)
            .WhereNotNull()
            .SelectLatest(feerate => investmentAppService.CreateInvestmentDraft(wallet.Id.Value, project.Info.Id, new Angor.Contexts.Funding.Projects.Domain.Amount(amountToOffer.Sats), new DomainFeerate(feerate!.Value)), isCalculatingDraft, scheduler: RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Publish();
        
        createDraft.HandleErrorsWith(uiServices.NotificationService, "Could not create investment preview").DisposeWith(disposable);
        
        IsCalculatingDraft = isCalculatingDraft.AsObservable().ObserveOn(RxApp.MainThreadScheduler);

        draftHelper = createDraft
            .Successes()
            .Select(d => new InvestmentDraft(investmentAppService, wallet, project, d))
            .ToProperty<DraftViewModel, IInvestmentDraft>(this, x => x.Draft)
            .DisposeWith(disposable);

        var canConfirm = this.WhenAnyValue<DraftViewModel, IInvestmentDraft>(model => model.Draft)
            .NotNull()
            .CombineLatest(IsCalculatingDraft, (hasDraft, calculating) => hasDraft && !calculating);

        Confirm = ReactiveCommand.CreateFromTask(() => Draft!.Confirm(), canConfirm).DisposeWith(disposable);
        Confirm.HandleErrorsWith(uiServices.NotificationService, "Could not send investment offer").DisposeWith(disposable);
        IsSending = Confirm.IsExecuting;
        feeHelper = this.WhenAnyValue<DraftViewModel, IAmountUI>(model => model.Draft!.TransactionFee).ToProperty(this, model => model.Fee).DisposeWith(disposable);
        createDraft.Connect().DisposeWith(disposable);
        SelectedPreset = Presets.FirstOrDefault(preset => preset.Name.Contains("Standard"));
    }

    public IFeeratePreset? SelectedPreset { get; set; }
    public IObservable<bool> IsCalculatingDraft { get; }
    public IObservable<bool> IsSending { get; }
    public ReactiveCommand<Unit, Result<Guid>> Confirm { get; }
    public IEnumerable<IFeeratePreset> Presets => uiServices.FeeratePresets;

    public void Dispose()
    {
        disposable.Dispose();
    }
}
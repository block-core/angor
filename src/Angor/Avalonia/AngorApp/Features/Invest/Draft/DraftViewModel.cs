using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Reactive;

namespace AngorApp.Features.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IDraftViewModel, IDisposable
{
    public IProject Project { get; }
    public IAmountUI AmountToOffer { get; }
    private readonly UIServices uiServices;
    [ObservableAsProperty] private IInvestmentDraft? draft;
    [Reactive] private long? feerate;
    [ObservableAsProperty] private IAmountUI? fee;
    private readonly BehaviorSubject<bool> isCalculatingDraft = new(false);
    private readonly CompositeDisposable disposable = new();

    public DraftViewModel(IInvestmentAppService investmentAppService, IWallet wallet, IAmountUI amountToOffer, IProject project, UIServices uiServices)
    {
        AmountToOffer = amountToOffer;
        Project = project;
        this.uiServices = uiServices;

        isCalculatingDraft.DisposeWith(disposable);

        var createDraft = this.WhenAnyValue(x => x.Feerate)
            .WhereNotNull()
            .SelectLatest(feerate => investmentAppService.CreateInvestmentDraft(wallet.Id.Value, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(amountToOffer.Sats), new DomainFeerate(feerate!.Value)), isCalculatingDraft, scheduler: RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Publish();
        
        createDraft.HandleErrorsWith(uiServices.NotificationService, "Could not create investment preview").DisposeWith(disposable);
        
        IsCalculatingDraft = isCalculatingDraft.AsObservable().ObserveOn(RxApp.MainThreadScheduler);

        draftHelper = createDraft
            .Successes()
            .Select(d => new InvestmentDraft(investmentAppService, wallet, project, d))
            .ToProperty(this, x => x.Draft)
            .DisposeWith(disposable);

        var canConfirm = this.WhenAnyValue(model => model.Draft)
            .NotNull()
            .CombineLatest(IsCalculatingDraft, (hasDraft, calculating) => hasDraft && !calculating);

        Confirm = ReactiveCommand.CreateFromTask(() => Draft!.Confirm(), canConfirm).DisposeWith(disposable);
        Confirm.HandleErrorsWith(uiServices.NotificationService, "Could not send investment offer").DisposeWith(disposable);
        IsSending = Confirm.IsExecuting;
        feeHelper = this.WhenAnyValue(model => model.Draft!.TransactionFee).ToProperty(this, model => model.Fee).DisposeWith(disposable);
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
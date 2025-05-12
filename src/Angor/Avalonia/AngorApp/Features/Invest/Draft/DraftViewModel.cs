using System.Reactive.Subjects;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Controls;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Reactive;

namespace AngorApp.Features.Invest.Draft;

public partial class DraftViewModel : ReactiveObject, IDraftViewModel
{
    private readonly UIServices uiServices;
    [ObservableAsProperty] private IInvestmentDraft? draft;
    [Reactive] private long? feerate;
    [ObservableAsProperty] private IAmountUI? fee;
    private readonly BehaviorSubject<bool> isCalculatingDraft = new(false);

    public DraftViewModel(IInvestmentAppService investmentAppService, IWallet wallet, long sats, IProject project, UIServices uiServices)
    {
        this.uiServices = uiServices;
        SatsToInvest = sats;

        draftHelper = this.WhenAnyValue(x => x.Feerate)
            .WhereNotNull()
            .SelectLatest(l => investmentAppService.CreateInvestmentDraft(wallet.Id.Value, new ProjectId(project.Id), new Angor.Contexts.Funding.Projects.Domain.Amount(sats)), isCalculatingDraft, TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Successes()
            .Select(d => new InvestmentDraft(investmentAppService, wallet, project, d))
            .ToProperty(this, x => x.Draft);

        var canConfirm = this.WhenAnyValue(model => model.Draft).NotNull().CombineLatest(isCalculatingDraft, (hasDraft, calculating) => hasDraft && !calculating);
        Confirm = ReactiveCommand.CreateFromTask(() => Draft!.Confirm(), canConfirm);
        IsSending = Confirm.IsExecuting;

        IsCalculating = isCalculatingDraft.AsObservable();
        FeeCalculator = new FeeCalculatorDesignTime();
        feeHelper = this.WhenAnyValue(model => model.Draft!.TotalFee).ToProperty(this, model => model.Fee);
    }

    public IObservable<bool> IsCalculating { get; }
    public IObservable<bool> IsSending { get; }
    public ReactiveCommand<Unit, Result<Guid>> Confirm { get; }
    public long SatsToInvest { get; }
    public IFeeCalculator FeeCalculator { get; }
    public IEnumerable<IFeeratePreset> Presets => uiServices.FeeratePresets;
}
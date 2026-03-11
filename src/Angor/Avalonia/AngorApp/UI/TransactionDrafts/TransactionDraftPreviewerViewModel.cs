using System.Reactive.Subjects;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.Shared.Controls.Feerate;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Reactive;

namespace AngorApp.UI.TransactionDrafts;

public partial class TransactionDraftPreviewerViewModel : ReactiveValidationObject, IValidatable
{
    private readonly UIServices uiServices;
    [Reactive] private long? selectedFeerate;
    [ObservableAsProperty] private IEnumerable<IFeeratePreset>? feerates;

    public TransactionDraftPreviewerViewModel(Func<long, Task<Result<ITransactionDraftViewModel>>> getDraft, Func<ITransactionDraftViewModel, Task<Result<Guid>>> commitDraft, UIServices uiServices, Func<Task<Result>>? refreshWallet = null)
    {
        this.uiServices = uiServices;
        var isGeneratingDraft = new BehaviorSubject<bool>(false);

        feeratesHelper = Observable.FromAsync(() => uiServices.GetFeeratePresetsAsync())
            .ToProperty(this, x => x.Feerates);

        // Wrap getDraft to refresh wallet UTXOs first if a refresh function is provided
        Func<long, Task<Result<ITransactionDraftViewModel>>> getDraftWithRefresh = refreshWallet != null
            ? async feerate =>
            {
                var refreshResult = await refreshWallet();
                if (refreshResult.IsFailure)
                    return Result.Failure<ITransactionDraftViewModel>($"Failed to refresh wallet: {refreshResult.Error}");
                return await getDraft(feerate);
            }
            : getDraft;

        var draftResults = this.WhenAnyValue(model => model.SelectedFeerate)
            .WhereNotNull()
            .SelectLatest(feerate => getDraftWithRefresh(feerate!.Value), isGeneratingDraft, TimeSpan.FromSeconds(0.2), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Publish()
            .RefCount();

        draftResults.HandleErrorsWith(uiServices.NotificationService, "Failed to create transaction draft");

        var drafts = draftResults.Successes();

        Draft = new Reactive.Bindings.ReactiveProperty<ITransactionDraftViewModel?>(drafts, initialValue: null);
        this.ValidationRule(model => model.Draft.Value, draft => draft is not null, "There is no draft to submit");

        IsGettingDraft = isGeneratingDraft.ObserveOn(RxApp.MainThreadScheduler);
        CommitDraft = EnhancedCommand.Create(() => commitDraft(Draft.Value!), Draft.NotNull().CombineLatest(IsGettingDraft, (hasDraft, gettingDraft) => hasDraft && !gettingDraft));
        CommitDraft.HandleErrorsWith(uiServices.NotificationService, "Failed to submit investment offer");
    }

    public IEnhancedCommand<Result<Guid>> CommitDraft { get; }
    public IObservable<bool> IsGettingDraft { get; }
    public Reactive.Bindings.ReactiveProperty<ITransactionDraftViewModel?> Draft { get; }
    public IObservable<bool> IsValid => this.IsValid();
    public IAmountUI? Amount { get; set; }
}
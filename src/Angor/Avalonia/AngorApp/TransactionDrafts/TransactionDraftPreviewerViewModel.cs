using System.Reactive.Subjects;
using AngorApp.TransactionDrafts.DraftTypes;
using AngorApp.UI.Controls.Feerate;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Reactive;

namespace AngorApp.TransactionDrafts;

public partial class TransactionDraftPreviewerViewModel : ReactiveValidationObject, IValidatable
{
    private readonly UIServices uiServices;
    [Reactive] private long? selectedFeerate;

    public TransactionDraftPreviewerViewModel(Func<long, Task<Result<ITransactionDraftViewModel>>> getDraft, Func<ITransactionDraftViewModel, Task<Result<Guid>>> commitDraft, UIServices uiServices)
    {
        this.uiServices = uiServices;
        var isGeneratingDraft = new BehaviorSubject<bool>(false);

        var draftResults = this.WhenAnyValue(model => model.SelectedFeerate)
            .WhereNotNull()
            .SelectLatest(feerate => getDraft(feerate!.Value), isGeneratingDraft, TimeSpan.FromSeconds(0.2), RxApp.MainThreadScheduler)
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
    public IEnumerable<IFeeratePreset> Feerates => uiServices.FeeratePresets;
    public IObservable<bool> IsValid => this.IsValid();
    public IAmountUI? Amount { get; set; }
}
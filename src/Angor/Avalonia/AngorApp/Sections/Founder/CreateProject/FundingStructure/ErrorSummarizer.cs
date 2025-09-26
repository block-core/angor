using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI.Validation.Contexts;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

/// <summary>
/// Aggregates validation error messages into a bindable collection.
///
/// Why this listens to per-component changes instead of the context aggregate:
/// - ValidationContext.ValidationStatusChange only fires when the overall Valid flag toggles
///   (true <-> false). If a component updates its message while the context remains invalid
///   (false -> false), the context-level stream will not emit.
/// - By observing each IValidationComponent.ValidationStatusChange we react to every message change,
///   regardless of whether the aggregate Valid changes.
///
/// Why the list is rebuilt on each tick instead of diffed:
/// - Using a SourceList with Clear + AddRange produces a correct initial materialization and
///   consistent updates without depending on a diff between previous and current snapshots.
/// </summary>
public sealed class ErrorSummarizer : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposable = new();

/// <summary>
/// Creates a new ErrorSummarizer bound to the provided validation context.
/// This eagerly materializes an initial snapshot and then updates on any component change.
/// </summary>
public ErrorSummarizer(IValidationContext validationContext)
    {
        // Bindable backing list for error messages (rebuilt on each change).
        var source = new SourceList<string>().DisposeWith(disposable);

        source.Connect()
            .Bind(out var errors)
            .Subscribe()
            .DisposeWith(disposable);

        // Observe per-component status changes (including initial snapshot) and compute current errors.
        // NOTE: We avoid context.ValidationStatusChange here because it only emits on aggregate Valid toggles,
        //       which misses message updates while the context stays invalid.
        var componentErrors = validationContext.Validations
            .Connect()
            .AutoRefreshOnObservable(c => c.ValidationStatusChange)
            .ToCollection()
            .Select(list =>
            {
                // Force activation by accessing IsValid/Text and materialize current messages per component.
                var errorsNow = list
                    .Where(c => !c.IsValid && c.Text is not null)
                    .SelectMany(c => c.Text!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();
                return errorsNow;
            });

        componentErrors
            .Subscribe(list =>
            {
                // Rebuild the list on every tick to ensure correct initial materialization
                // and consistent updates without relying on diffs.
                source.Edit(updater =>
                {
                    updater.Clear();
                    updater.AddRange(list);
                });
            })
            .DisposeWith(disposable);

        Errors = errors;
    }

    public ReadOnlyObservableCollection<string> Errors { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
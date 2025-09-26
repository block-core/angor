using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI.Validation.Contexts;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public sealed class ErrorSummarizer : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public ErrorSummarizer(IValidationContext validationContext)
    {
        validationContext.ValidationStatusChange
            .Select(state => state.Text.ToList())
            .StartWith(validationContext.Text.ToList())
            .EditDiff(s => s)
            .Bind(out var errors)
            .Subscribe()
            .DisposeWith(disposable);
        
        Errors = errors;
    }

    public ReadOnlyObservableCollection<string> Errors { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
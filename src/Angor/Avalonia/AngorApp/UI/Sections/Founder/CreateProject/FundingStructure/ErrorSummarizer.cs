using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ReactiveUI.Validation.Contexts;

namespace AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;

public sealed class ErrorSummarizer : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public ErrorSummarizer(IValidationContext validationContext)
    {
        var source = new SourceList<string>().DisposeWith(disposable);

        source.Connect()
              .Bind(out var errors)
              .Subscribe()
              .DisposeWith(disposable);

        var componentErrors = validationContext.Validations
                                               .Connect()
                                               .AutoRefreshOnObservable(c => c.ValidationStatusChange)
                                               .ToCollection()
                                               .Select(list =>
                                               {
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
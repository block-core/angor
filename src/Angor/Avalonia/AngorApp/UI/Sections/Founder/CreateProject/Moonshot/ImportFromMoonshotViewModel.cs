using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.CreateProject.Moonshot;

public partial class ImportFromMoonshotViewModel : ReactiveValidationObject, IImportFromMoonshotViewModel, IDisposable
{
    private readonly IMoonshotService _moonshotService;
    private readonly CompositeDisposable _disposable = new();

    [Reactive] private string? eventId;
    [Reactive] private bool isLoading;
    [Reactive] private string? errorMessage;

    public ImportFromMoonshotViewModel(IMoonshotService moonshotService)
    {
        _moonshotService = moonshotService;

        // Validation rules
        this.ValidationRule(x => x.EventId, 
            x => !string.IsNullOrWhiteSpace(x), 
            "Nostr Event ID is required.")
            .DisposeWith(_disposable);

        this.ValidationRule(x => x.EventId,
            x => string.IsNullOrWhiteSpace(x) || x.Trim().Length == 64,
            "Event ID must be a 64-character hex string.")
            .DisposeWith(_disposable);

        // Create the import command
        var canImport = this.WhenAnyValue(x => x.EventId, x => x.IsLoading)
            .Select(tuple => !string.IsNullOrWhiteSpace(tuple.Item1) && 
                             tuple.Item1.Trim().Length == 64 && 
                             !tuple.Item2);

        Import = ReactiveCommand.CreateFromTask(ExecuteImport, canImport).Enhance();

        // Clear error message when event ID changes
        this.WhenAnyValue(x => x.EventId)
            .Subscribe(_ => ErrorMessage = null)
            .DisposeWith(_disposable);
    }

    private async Task<Result<MoonshotProjectData>> ExecuteImport()
    {
        if (string.IsNullOrWhiteSpace(EventId))
        {
            return Result.Failure<MoonshotProjectData>("Event ID is required.");
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _moonshotService.GetMoonshotProjectAsync(EventId.Trim());

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
            }

            return result;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public IEnhancedCommand<Result<MoonshotProjectData>> Import { get; }

    public IObservable<bool> IsValid => this.IsValid();

    public void Dispose()
    {
        _disposable.Dispose();
    }
}

using System.Reactive.Disposables;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Sections.Founder.CreateProject.Moonshot;

public partial class ImportFromMoonshotViewModel : ReactiveValidationObject, IImportFromMoonshotViewModel, IDisposable
{
    private readonly IFounderAppService _founderAppService;
    private readonly CompositeDisposable _disposable = new();

    [Reactive] private string? eventId;
    [Reactive] private bool isLoading;
    [Reactive] private string? errorMessage;

    public ImportFromMoonshotViewModel(IFounderAppService founderAppService)
    {
        _founderAppService = founderAppService;

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
            var result = await _founderAppService.GetMoonshotProject(new GetMoonshotProject.GetMoonshotProjectRequest(EventId.Trim()));

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                return Result.Failure<MoonshotProjectData>(result.Error);
            }

            return Result.Success(result.Value.MoonshotProjectData);
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

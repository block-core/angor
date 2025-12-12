using System.Linq.Expressions;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Dtos;
using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Sections.Founder.CreateProject.Moonshot;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.Founder.CreateProject.Profile;

public partial class ProfileViewModel : ReactiveValidationObject, IProfileViewModel
{
    private readonly UIServices uiServices;
    private readonly IFounderAppService _founderAppService;
    private readonly Action<MoonshotProjectData>? _onMoonshotImported;
    private readonly CompositeDisposable disposable = new();
    [Reactive] private string? avatarUri;
    [Reactive] private string? bannerUri;
    [Reactive] private string? description;
    [Reactive] private string? projectName;
    [Reactive] private string? websiteUri;
    [Reactive] private string? nip05Username;
    [Reactive] private string? lightningAddress;

    public ProfileViewModel(
        ProjectSeedDto projectSeed,
        UIServices uiServices,
        IFounderAppService founderAppService,
        Action<MoonshotProjectData>? onMoonshotImported = null)
    {
        this.uiServices = uiServices;
        _founderAppService = founderAppService;
        _onMoonshotImported = onMoonshotImported;
#if DEBUG
        AvatarUri = "https://picsum.photos/170/170";
        BannerUri = "https://picsum.photos/800/312";
        ProjectName = "Test project";
        Description = "Test description";
        WebsiteUri = "https://sample.com";
#endif

        // PROJECT NAME VALIDATIONS (ALWAYS)
        this.ValidationRule(x => x.ProjectName, x => !string.IsNullOrWhiteSpace(x), "Project name is required.").DisposeWith(disposable);
        this.ValidationRule(x => x.ProjectName, x => string.IsNullOrWhiteSpace(x) || x.Length <= 200, "Project name must not exceed 200 characters.").DisposeWith(disposable);

        // PROJECT DESCRIPTION VALIDATIONS (ALWAYS)
        this.ValidationRule(x => x.Description, x => !string.IsNullOrWhiteSpace(x), "Project description is required.").DisposeWith(disposable);
        this.ValidationRule(x => x.Description, x => string.IsNullOrWhiteSpace(x) || x.Length <= 400, "Project description must not exceed 400 characters.").DisposeWith(disposable);

        // WEBSITE URL VALIDATIONS (ALWAYS - Optional but must be valid if provided)
        this.ValidationRule(x => x.WebsiteUri, x => string.IsNullOrWhiteSpace(x) || (Uri.TryCreate(x, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)), "Please enter a valid URL (starting with http:// or https://)").DisposeWith(disposable);

        this.ValidationRule(x => x.AvatarUri, IsValidImage(model => model.AvatarUri), result => result.IsSuccess, result => $"Invalid image: {result}").DisposeWith(disposable);
        this.ValidationRule(x => x.BannerUri, IsValidImage(model => model.BannerUri), result => result.IsSuccess, result => $"Invalid image: {result}").DisposeWith(disposable);

        // NIP-05 and Lightning Address validations (async)
        var isValidNip05Username = this.WhenAnyValue(model => model.Nip05Username)
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .Select(s => Observable.FromAsync(() => string.IsNullOrWhiteSpace(s) ? Task.FromResult(Result.Success()) : uiServices.Validations.CheckNip05Username(s, projectSeed.NostrPubKey)))
            .Switch();

        var isValidLightningAddress = this.WhenAnyValue(model => model.LightningAddress)
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .Select(s => Observable.FromAsync(() => string.IsNullOrWhiteSpace(s) ? Task.FromResult(Result.Success()) : uiServices.Validations.CheckLightningAddress(s)))
            .Switch();

        this.ValidationRule(x => x.Nip05Username, isValidNip05Username, x => x.IsSuccess, error => error.Error).DisposeWith(disposable);
        this.ValidationRule(x => x.LightningAddress, isValidLightningAddress, x => x.IsSuccess, error => error.Error).DisposeWith(disposable);

        Errors = new ErrorSummarizer(ValidationContext).DisposeWith(disposable).Errors;

        // Import from Moonshot command
        ImportFromMoonshot = ReactiveCommand.CreateFromTask(ExecuteImportFromMoonshot).Enhance();
    }

    private async Task<Result> ExecuteImportFromMoonshot()
    {
        var importViewModel = new ImportFromMoonshotViewModel(_founderAppService);

        // ShowAndGetResult returns Maybe<TValue> where TValue is the unwrapped success value from the command
        // The Import command returns Result<MoonshotProjectData>
        // The dialog shows an error if the Result fails, and returns Maybe<MoonshotProjectData>
        Maybe<Angor.Contexts.Funding.Founder.Dtos.MoonshotProjectData> dialogResult = await uiServices.Dialog.ShowAndGetResult(
                  importViewModel,
                  "Import from Moonshot",
                  vm => vm.Import.Enhance("Import"));

        if (!dialogResult.HasValue)
        {
            return Result.Failure("Import cancelled");
        }

        Angor.Contexts.Funding.Founder.Dtos.MoonshotProjectData moonshotData = dialogResult.Value;

        // Populate fields from Moonshot data
        ProjectName = moonshotData.Moonshot.Title;
        Description = moonshotData.Moonshot.Content;

        // Store Moonshot data for FundingStructure to access
        LastImportedMoonshotData = moonshotData;

        // Notify FundingStructureViewModel about the imported data
        _onMoonshotImported?.Invoke(moonshotData);

        await uiServices.NotificationService.Show($"Imported project: {moonshotData.Moonshot.Title}", "Import Successful");

        return Result.Success();
    }

    /// <summary>
    /// Gets the last imported Moonshot data for use by FundingStructureViewModel.
    /// </summary>
    public Angor.Contexts.Funding.Founder.Dtos.MoonshotProjectData? LastImportedMoonshotData { get; private set; }

    /// <summary>
    /// Gets the command to import from Moonshot.
    /// </summary>
    public IEnhancedCommand<Result> ImportFromMoonshot { get; }

    private IObservable<Result<bool>> IsValidImage(Expression<Func<ProfileViewModel, string?>> expression)
    {
        return this.WhenAnyValue(expression)
                .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
                .Select(uri => Observable.FromAsync(() => uiServices.Validations.IsImage(uri)))
                .Switch()
                .ObserveOn(RxApp.MainThreadScheduler);
    }

    public ICollection<string> Errors { get; }
    public IObservable<bool> IsValid => this.IsValid();

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
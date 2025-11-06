using System.Reactive.Disposables;
using System.Threading.Tasks;
using AngorApp.Flows;
using AngorApp.Flows.CreateProject;
using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Founder.CreateProject.Profile;

public partial class ProfileViewModel : ReactiveValidationObject, IProfileViewModel, IHaveErrors
{
    private readonly UIServices uiServices;
    private readonly CompositeDisposable disposable = new();
    [Reactive] private string? avatarUri;
    [Reactive] private string? bannerUri;
    [Reactive] private string? description;
    [Reactive] private string? projectName;
    [Reactive] private string? websiteUri;
    [Reactive] private string? nip05Username;
    [Reactive] private string? lightningAddress;

    public ProfileViewModel(CreateProjectFlow.ProjectSeed projectSeed, UIServices uiServices)
    {
        this.uiServices = uiServices;
#if DEBUG
        AvatarUri = "https://picsum.photos/170/170"; // Placeholder for avatar
        BannerUri = "https://picsum.photos/800/312"; // Placeholder for banner
        ProjectName = "Test project"; // Placeholder for banner
        WebsiteUri = "https://sample.com"; // Placeholder for website
#endif

        this.ValidationRule(x => x.ProjectName, x => !string.IsNullOrEmpty(x), "Project name cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.Description, x => !string.IsNullOrEmpty(x), "Description cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.WebsiteUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Website cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.BannerUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Banner cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.AvatarUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Avatar Cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.WebsiteUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid website URL").DisposeWith(disposable);
        this.ValidationRule(x => x.BannerUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid banner URL").DisposeWith(disposable);
        this.ValidationRule(x => x.AvatarUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid avatar URL").DisposeWith(disposable);

        var isValidNip05Username = this.WhenAnyValue(model => model.Nip05Username)
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .Select(s => Observable.FromAsync(() => s == null ? Task.FromResult(Result.Success()) : uiServices.Validations.CheckNip05Username(s, projectSeed.NostrPubKey)))
            .Switch();
        
        var isValidLightningAddress = this.WhenAnyValue(model => model.LightningAddress)
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .Select(s => Observable.FromAsync(() => s == null ? Task.FromResult(Result.Success()) : uiServices.Validations.CheckLightningAddress(s)))
            .Switch();
        
        this.ValidationRule(x => x.Nip05Username, isValidNip05Username, x => x.IsSuccess, error => error.Error).DisposeWith(disposable);
        
        this.ValidationRule(x => x.LightningAddress, isValidLightningAddress, x => x.IsSuccess, error => error.Error).DisposeWith(disposable);

        Errors = new ErrorSummarizer(ValidationContext).DisposeWith(disposable).Errors;
    }

    public ICollection<string> Errors { get; }

    public IObservable<bool> IsValid => this.IsValid();

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
using System.Reactive.Disposables;
using System.Threading.Tasks;
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

    public ProfileViewModel(UIServices uiServices)
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
            .WhereNotNull()
            .Throttle(TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .Select(s => Observable.FromAsync(() => uiServices.Validations.IsValidNip05Username(s, "abar")))
            .Switch();
        
        this.ValidationRule(isValidNip05Username, x => x.IsSuccess && x.Value, error => error.IsSuccess ? $"Invalid NIP-05 username" : $"Could not validate NIP-05 username: {error.Error}").DisposeWith(disposable);
        //this.ValidationRule(x => x.LightningAddress, x => x is null || IsValidLightningAddress(x), "Invalid Lighting address").DisposeWith(disposable);

        Errors = new ErrorSummarizer(ValidationContext).DisposeWith(disposable).Errors;
    }

    private Task<Result<bool>> IsValidLightningAddress(string address)
    {
        return uiServices.Validations.IsValidLightningAddress(address);
    }

    private Task<Result<bool>> IsValidNip05Username(string username, string nostrPubKey)
    {
        return uiServices.Validations.IsValidNip05Username(username, nostrPubKey);
    }

    public ICollection<string> Errors { get; }

    public IObservable<bool> IsValid => this.IsValid();

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
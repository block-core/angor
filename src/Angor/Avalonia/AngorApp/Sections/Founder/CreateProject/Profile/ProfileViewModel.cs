using System.Reactive.Disposables;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Founder.CreateProject.Profile;

public partial class ProfileViewModel : ReactiveValidationObject, IProfileViewModel
{
    [Reactive] private string? websiteUri;
    [Reactive] private string? description;
    [Reactive] private string? avatarUri;
    [Reactive] private string? bannerUri;
    [Reactive] private string? projectName;

    private readonly CompositeDisposable disposable = new();
    
    public ProfileViewModel()
    {
        this.AvatarUri = "https://picsum.photos/170/170"; // Placeholder for avatar
        this.BannerUri = "https://picsum.photos/800/312px"; // Placeholder for banner
        
        this.ValidationRule(x => x.ProjectName, x => !string.IsNullOrEmpty(x), "Cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.Description, x => !string.IsNullOrEmpty(x), "Cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.WebsiteUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.BannerUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.AvatarUri, x => !string.IsNullOrEmpty(x) && !string.IsNullOrEmpty(x), "Cannot be empty").DisposeWith(disposable);
        this.ValidationRule(x => x.WebsiteUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid URL").DisposeWith(disposable);
        this.ValidationRule(x => x.BannerUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid URL").DisposeWith(disposable);
        this.ValidationRule(x => x.AvatarUri, x => string.IsNullOrWhiteSpace(x) || Uri.TryCreate(x, UriKind.Absolute, out _), "Invalid URL").DisposeWith(disposable);
    }
    
    public IObservable<bool> IsValid => this.IsValid();

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
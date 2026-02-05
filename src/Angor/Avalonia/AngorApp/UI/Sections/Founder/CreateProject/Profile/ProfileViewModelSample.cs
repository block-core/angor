using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Shared.Controls.ImageUploadWizard;

namespace AngorApp.UI.Sections.Founder.CreateProject.Profile;

public class ProfileViewModelSample : IProfileViewModel
{
    public IObservable<bool> IsValid { get; set; } = Observable.Return(true);
    public string? ProjectName { get; set; }
    public string? WebsiteUri { get; set; }
    public string? Description { get; set; }
    public string? AvatarUri { get; set; }
    public string? BannerUri { get; set; }
    public string? Nip05Username { get; set; }
    public string? LightningAddress { get; set; }
    public ICollection<string> Errors { get; set; } = new List<string>();
    public IEnhancedCommand<Result> ImportFromMoonshot { get; } = ReactiveCommand.Create(() => Result.Success()).Enhance();
    public MoonshotProjectData? LastImportedMoonshotData { get; } = null;
    public IImageUploadWizardViewModel BannerUploadWizard { get; } = new ImageUploadWizardViewModelSample();
    public IImageUploadWizardViewModel AvatarUploadWizard { get; } = new ImageUploadWizardViewModelSample();
}
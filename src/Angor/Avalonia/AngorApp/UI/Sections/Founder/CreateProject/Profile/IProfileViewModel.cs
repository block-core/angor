using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Shared.Controls.ImageUploadWizard;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.CreateProject.Profile;

public interface IProfileViewModel : IHaveErrors
{
    public IObservable<bool> IsValid { get; }
    public string? ProjectName { get; set; }
    string? WebsiteUri { get; set; }
    string? Description { get; set; }
    string? AvatarUri { get; set; }
    string? BannerUri { get; set; }
    public string? Nip05Username { get; set; }
    public string? LightningAddress { get; set; }
    
    /// <summary>
    /// Gets the view model for the banner image upload wizard.
    /// </summary>
    IImageUploadWizardViewModel BannerUploadWizard { get; }
    
    /// <summary>
    /// Gets the view model for the avatar image upload wizard.
    /// </summary>
    IImageUploadWizardViewModel AvatarUploadWizard { get; }
    
    IEnhancedCommand<Result> ImportFromMoonshot { get; }
    
    MoonshotProjectData? LastImportedMoonshotData { get; }
}
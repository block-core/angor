using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
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
    
    IEnhancedCommand<Result> ImportFromMoonshot { get; }
    
    MoonshotProjectData? LastImportedMoonshotData { get; }
}
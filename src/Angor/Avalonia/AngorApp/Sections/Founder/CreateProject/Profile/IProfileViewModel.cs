using AngorApp.Sections.Founder.CreateProject.FundingStructure;

namespace AngorApp.Sections.Founder.CreateProject.Profile;

public interface IProfileViewModel : IHaveErrors
{
    public IObservable<bool> IsValid { get; }
    public string? ProjectName { get; set; }
    string? WebsiteUri { get; set; }
    string? Description { get; set; }
    string? AvatarUri { get; set; }
    string? BannerUri { get; set; }
    public string? Nip05Username { get; set; }
}
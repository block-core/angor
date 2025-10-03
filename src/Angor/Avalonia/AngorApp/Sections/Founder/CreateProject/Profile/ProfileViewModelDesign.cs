namespace AngorApp.Sections.Founder.CreateProject.Profile;

public class ProfileViewModelDesign : IProfileViewModel
{
    public IObservable<bool> IsValid { get; set; }
    public string? ProjectName { get; set; }
    public string? WebsiteUri { get; set; }
    public string? Description { get; set; }
    public string? AvatarUri { get; set; }
    public string? BannerUri { get; set; }
    public string? Nip05Username { get; set; }
    public string? LightningAddress { get; set; }
    public ICollection<string> Errors { get; set; }
}
namespace AngorApp.Sections.Founder.CreateProject.Profile;

public interface IProfileViewModel
{
    public IObservable<bool> IsValid { get; }
    public string? ProjectName { get; set; }
    string? WebsiteUri { get; set; }
    string? Description { get; set; }
    string? AvatarUri { get; set; }
    string? BannerUri { get; set; }
}

public class ProfileViewModelDesign : IProfileViewModel
{
    public IObservable<bool> IsValid { get; set; }
    public string? ProjectName { get; set; }
    public string? WebsiteUri { get; set; }
    public string? Description { get; set; }
    public string? AvatarUri { get; set; }
    public string? BannerUri { get; set; }
}
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModelDesign : ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; set; } = new List<ICreateProjectStage>();
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public IEnhancedCommand AddStage { get; }
    public int? PenaltyDays { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public IEnhancedCommand Create { get; }
    public string? WebsiteUri { get; set; }
    public string? Description { get; set; }
    public string? AvatarUri { get; set; }
    public string? BannerUri { get; set; }
    public string? ProjectName { get; set; }
    public long? Sats { get; set; }
}
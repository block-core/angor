using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
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
    public IObservable<bool> IsValid { get; set; }
    public string? WebsiteUri { get; set; }
    public string? Description { get; set; }
    public string? AvatarUri { get; set; }
    public string? BannerUri { get; set; }
    public string? ProjectName { get; set; }
    public long? Sats { get; set; }
    public IStagesViewModel StagesViewModel { get; set; }
    public IProfileViewModel ProfileViewModel { get; set; }
    public IFundingStructureViewModel FundingStructureViewModel { get; set; }
}
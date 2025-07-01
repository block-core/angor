using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public interface ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; }
    public DateTime StartDate { get; }
    public DateTime? EndDate { get; set; }
    public IEnhancedCommand AddStage { get; }
    public int? PenaltyDays { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
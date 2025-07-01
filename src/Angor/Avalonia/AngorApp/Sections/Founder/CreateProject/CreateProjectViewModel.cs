using DynamicData;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveObject, ICreateProjectViewModel
{
    private readonly SourceList<ICreateProjectStage> stages;

    public CreateProjectViewModel()
    {
        stages = new SourceList<ICreateProjectStage>();
        AddStage = ReactiveCommand.Create(() => stages.Add(new CreateProjectStage(stage => stages.Remove(stage)))).Enhance();
    }
    
    public IEnumerable<ICreateProjectStage> Stages { get; } = [];
    public DateTime StartDate { get; }
    public DateTime? EndDate { get; set; }
    public IEnhancedCommand AddStage { get; }
    public int? PenaltyDays { get; set; }
    public DateTime? ExpiryDate { get; }
}
using DynamicData;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModel : ReactiveObject, ICreateProjectViewModel
{
    private readonly SourceList<ICreateProjectStage> stagesSource;

    public CreateProjectViewModel()
    {
        stagesSource = new SourceList<ICreateProjectStage>();
        AddStage = ReactiveCommand.Create(() => stagesSource.Add(CreateStage())).Enhance();
        stagesSource.Connect()
            .Bind(out var stages)
            .Subscribe();

        stagesSource.Add(CreateStage());
        Stages = stages;
    }

    private CreateProjectStage CreateStage()
    {
        return new CreateProjectStage(stage => stagesSource.Remove(stage));
    }

    public IEnumerable<ICreateProjectStage> Stages { get; }
    public DateTime StartDate { get; } = DateTime.Now;
    public DateTime? EndDate { get; set; } 
    public IEnhancedCommand AddStage { get; }
    public int? PenaltyDays { get; set; } = 60;
    public DateTime? ExpiryDate { get; set; }
}
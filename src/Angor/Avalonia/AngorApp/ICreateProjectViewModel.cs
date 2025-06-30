using DynamicData;
using Zafiro.UI.Commands;

namespace AngorApp;

public interface ICreateProjectViewModel
{
    public IEnumerable<ICreateProjectStage> Stages { get; }
    public DateTime StartDate { get; }
    public DateTime? EndDate { get; set; }
    public IEnhancedCommand AddStage { get; }
    public int? PenaltyDays { get; set; }
    public DateTime? ExpiryDate { get; }
}

public interface ICreateProjectStage
{
    public double Percent { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public IEnhancedCommand Remove { get; }
}

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

public class CreateProjectStage : ICreateProjectStage
{
    public CreateProjectStage(Action<ICreateProjectStage> remove)
    {
        Remove = ReactiveCommand.Create(() => remove(this)).Enhance();
    }

    public double Percent { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public IEnhancedCommand Remove { get; }
}
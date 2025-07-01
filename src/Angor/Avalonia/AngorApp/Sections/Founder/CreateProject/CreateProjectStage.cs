using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

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
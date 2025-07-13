using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public class StagesViewModelDesign : IStagesViewModel
{
    public IEnhancedCommand AddStage { get; }
    public ICollection<ICreateProjectStage> Stages { get; } = new List<ICreateProjectStage>();
    public IObservable<bool> IsValid { get; }
}
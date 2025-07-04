using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public interface IStagesViewModel
{
    IEnhancedCommand AddStage { get; }
    ICollection<ICreateProjectStage> Stages { get; }
    IObservable<bool> IsValid { get; }
}
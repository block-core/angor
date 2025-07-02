using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public interface IStagesViewModel : IDisposable
{
    IEnhancedCommand AddStage { get; }
    IObservable<bool> IsValid { get; }
    IEnumerable<ICreateProjectStage> Stages { get; }
}
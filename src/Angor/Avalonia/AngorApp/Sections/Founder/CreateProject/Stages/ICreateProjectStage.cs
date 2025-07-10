using ReactiveUI.Validation.Abstractions;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public interface ICreateProjectStage : IReactiveObject, IValidatableViewModel
{
    public double? Percent { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public IEnhancedCommand Remove { get; }
}
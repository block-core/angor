using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public class CreateProjectStageDesign : ReactiveValidationObject, ICreateProjectStage
{
    public double? Percent { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public IEnhancedCommand Remove { get; }
}
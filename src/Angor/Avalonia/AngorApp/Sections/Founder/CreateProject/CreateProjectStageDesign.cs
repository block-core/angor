using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectStageDesign : ReactiveValidationObject, ICreateProjectStage
{
    public double? Percent { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public IEnhancedCommand Remove { get; }
    public int Index { get; set; }
}
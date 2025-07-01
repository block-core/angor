using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public partial class CreateProjectStage : ReactiveValidationObject, ICreateProjectStage
{
    [Reactive]
    private DateTimeOffset? startDate;

    [Reactive]
    private double? percent;

    public CreateProjectStage(Action<ICreateProjectStage> remove)
    {
        Remove = ReactiveCommand.Create(() => remove(this)).Enhance();
        this.ValidationRule(stage => stage.Percent, x => x is >= 1, "Percent must be greater than 1");
        this.ValidationRule(stage => stage.Percent, x => x != null, "Enter a percentage");
        this.ValidationRule(stage => stage.StartDate, x => x != null, "Enter a date");
        StartDate = DateTimeOffset.Now;
        Percent = 1;
    }

    public IEnhancedCommand Remove { get; }
}
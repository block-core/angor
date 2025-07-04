using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public partial class CreateProjectStage : ReactiveValidationObject, ICreateProjectStage
{
    [Reactive]
    private DateTime? releaseDate;

    [Reactive]
    private double? percent;

    public CreateProjectStage(Action<ICreateProjectStage> remove)
    {
        Remove = ReactiveCommand.Create(() => remove(this)).Enhance();
        this.ValidationRule(stage => stage.Percent, x => x is >= 1, "Percent must be greater than 1");
        this.ValidationRule(stage => stage.Percent, x => x != null, "Enter a percentage");
        this.ValidationRule(stage => stage.ReleaseDate, x => x != null, "Enter a relase date");
    }

    public IEnhancedCommand Remove { get; }
}
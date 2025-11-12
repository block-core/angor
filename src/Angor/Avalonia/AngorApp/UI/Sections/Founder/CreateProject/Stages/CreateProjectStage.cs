using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.CreateProject.Stages;

public partial class CreateProjectStage : ReactiveValidationObject, ICreateProjectStage
{
    [Reactive]
    private DateTime? releaseDate;

    [Reactive]
    private decimal? percent;

    public CreateProjectStage(Action<ICreateProjectStage> remove, IObservable<DateTime?> fundingEndDate)
    {
        Remove = ReactiveCommand.Create(() => remove(this)).Enhance();
   
        // STAGE PERCENTAGE VALIDATIONS (Always enforced)
        // Must be > 0%
        this.ValidationRule(stage => stage.Percent, x => x > 0, "Each stage must release more than 0% of funds.");
        // Must be ? 100%
        this.ValidationRule(stage => stage.Percent, x => x is null || x <= 100, "No stage can release more than 100% of funds.");
        // Required
        this.ValidationRule(stage => stage.Percent, x => x != null, "Stage percentage is required.");

        // STAGE DATE VALIDATIONS (Always enforced)
        // Required
        this.ValidationRule(stage => stage.ReleaseDate, x => x != null, "Stage release date is required.");

        // Must be after funding end date (Always enforced)
        var isAfterEndDate = this.WhenAnyValue(stage => stage.ReleaseDate)
            .CombineLatest(fundingEndDate, (release, end) => release != null && end != null && release > end);

        this.ValidationRule(x => x.ReleaseDate, isAfterEndDate, b => b, _ => "Stage release date must be after funding end date.");
    }

    public IEnhancedCommand Remove { get; }
}
using System.Reactive.Disposables;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Sections.Founder.CreateProject.Stages.Creator;

public partial class StagesCreatorViewModel : ReactiveValidationObject, IStagesCreatorViewModel
{
    [Reactive] private int? numberOfStages;
    [Reactive] private DateTime? selectedInitialDate;
    [Reactive] private PaymentFrequency? selectedFrequency;
    private readonly CompositeDisposable disposable = new();

    public StagesCreatorViewModel()
    {
        this.ValidationRule(x => x.NumberOfStages, n => n.HasValue && n > 0, "Number of stages must be greater than zero").DisposeWith(disposable);
        this.ValidationRule(x => x.SelectedInitialDate, n => n.HasValue && n.Value.Date >= DateTime.Today, "Initial date should be today or in the future").DisposeWith(disposable);
        this.ValidationRule(x => x.SelectedFrequency, n => n.HasValue, "You should select a frequency").DisposeWith(disposable);
    }

    protected override void Dispose(bool disposing)
    {
        disposable.Dispose();
        base.Dispose(disposing);
    }
}
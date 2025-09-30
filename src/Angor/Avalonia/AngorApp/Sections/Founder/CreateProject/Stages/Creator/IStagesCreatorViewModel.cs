using ReactiveUI.Validation.Abstractions;

namespace AngorApp.Sections.Founder.CreateProject.Stages.Creator;

public interface IStagesCreatorViewModel : IValidatableViewModel, IReactiveObject, IDisposable
{
    public int? NumberOfStages { get; set; }
    public DateTime? SelectedInitialDate  { get; set; }
    public PaymentFrequency? SelectedFrequency { get; set; }
}
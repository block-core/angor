using ReactiveUI.Validation.Abstractions;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public interface IPayoutConfig : IValidatableViewModel
    {
        decimal? Percent { get; set; }
        DateTime? PayoutDate { get; set; }
        IObservable<bool> IsValid { get; }
    }
}

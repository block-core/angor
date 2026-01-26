using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject
{
    public interface IGoalViewModel : IHaveTitle, IValidatable
    {
        IFundProjectConfig FundProject { get; }
        long? SelectedPresetSats { get; set; }
        IEnumerable<IAmountUI> AmountPresets { get; }
        decimal? ThresholdBtc { get; set; }
    }
}

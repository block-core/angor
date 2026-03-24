using AngorApp.Model.Amounts;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject
{
    public class GoalViewModelSample : IGoalViewModel
    {
        public IFundProjectConfig FundProject { get; } = new FundProjectConfigSample();
        public long? SelectedPresetSats { get; set; } = AmountUI.FromBtc(1).Sats;
        public IEnumerable<IAmountUI> AmountPresets { get; } =
        [
            AmountUI.FromBtc(0.25),
            AmountUI.FromBtc(0.5),
            AmountUI.FromBtc(1),
            AmountUI.FromBtc(2.5),
        ];

        public IObservable<string> Title => Observable.Return("Goal");
        public IObservable<bool> IsValid => Observable.Return(true);
        public decimal? ThresholdBtc { get; set; } = 0.001m;
    }
}

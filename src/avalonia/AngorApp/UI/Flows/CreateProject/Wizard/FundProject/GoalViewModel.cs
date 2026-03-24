using System.Linq;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;
using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject
{
    public class GoalViewModel : ReactiveObject, IHaveTitle, IGoalViewModel, IValidatable
    {

        private const decimal DefaultThresholdBtc = 0.001m;
        private const long SatsPerBtc = 100_000_000;

        public IFundProjectConfig FundProject { get; }

        public GoalViewModel(IFundProjectConfig fundProject)
        {
            FundProject = fundProject;

            if (FundProject.GoalAmount == null)
            {
                var defaultPreset = AmountPresets.FirstOrDefault();
                if (defaultPreset != null)
                {
                    FundProject.GoalAmount = new AmountUI(defaultPreset.Sats);
                }
            }


            if (FundProject.Threshold == null)
            {
                FundProject.Threshold = new AmountUI((long)(DefaultThresholdBtc * SatsPerBtc));
            }
        }


        public long? SelectedPresetSats
        {
            get => FundProject.GoalAmount?.Sats;
            set
            {
                if (value.HasValue)
                {
                    FundProject.GoalAmount = new AmountUI(value.Value);
                }
            }
        }


        public decimal? ThresholdBtc
        {
            get => FundProject.Threshold?.Btc;
            set
            {
                var btc = value ?? DefaultThresholdBtc;
                var sats = (long)(btc * SatsPerBtc);
                FundProject.Threshold = new AmountUI(sats);
                this.RaisePropertyChanged();
            }
        }

        public IObservable<string> Title => Observable.Return("Goal");

        public IEnumerable<IAmountUI> AmountPresets { get; } =
        [
            AmountUI.FromBtc(0.25),
            AmountUI.FromBtc(0.5),
            AmountUI.FromBtc(1),
            AmountUI.FromBtc(2.5),
        ];

        public IObservable<bool> IsValid => this.FundProject.WhenValid(
            x => x.GoalAmount
        );
    }
}

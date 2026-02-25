using System.Linq;
using System.Reactive.Disposables;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{

    public class FundingConfigurationViewModel : ReactiveObject, IHaveTitle, IFundingConfigurationViewModel, IValidatable
    {
        public IInvestmentProjectConfig NewProject { get; }
        private readonly CompositeDisposable disposable = new();

        public int MinPenaltyDays { get; }
        public int MaxPenaltyDays { get; }

        public FundingConfigurationViewModel(IInvestmentProjectConfig newProject, ValidationEnvironment environment = ValidationEnvironment.Production)
        {
            NewProject = newProject;
            MinPenaltyDays = environment == ValidationEnvironment.Debug ? 0 : 30;
            MaxPenaltyDays = environment == ValidationEnvironment.Debug ? 365 : 180;

            if (NewProject.StartDate == null)
            {
                NewProject.StartDate = DateTime.Now;
            }

            if (NewProject.TargetAmount == null)
            {
                var defaultPreset = AmountPresets.FirstOrDefault();
                if (defaultPreset != null)
                {
                    NewProject.TargetAmount = new AmountUI(defaultPreset.Sats);
                }
            }
        }


        public long? SelectedPresetSats
        {
            get => NewProject.TargetAmount?.Sats;
            set
            {
                if (value.HasValue)
                {
                    NewProject.TargetAmount = new AmountUI(value.Value);
                }
            }
        }

        public IObservable<string> Title => Observable.Return("Funding Configuration");

        public IEnumerable<IAmountUI> AmountPresets { get; } =
        [
            AmountUI.FromBtc(0.25),
            AmountUI.FromBtc(0.5),
            AmountUI.FromBtc(1),
            AmountUI.FromBtc(2.5),
        ];

        public IObservable<bool> IsValid => this.NewProject.WhenValid(
            x => x.TargetAmount,
            x => x.StartDate,
            x => x.FundingEndDate,
            x => x.PenaltyDays
        );
    }
}

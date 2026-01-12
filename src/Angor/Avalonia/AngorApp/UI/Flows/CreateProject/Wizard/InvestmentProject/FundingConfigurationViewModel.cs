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

        public FundingConfigurationViewModel(IInvestmentProjectConfig newProject)
        {
            NewProject = newProject;

            if (NewProject.StartDate == null)
            {
                NewProject.StartDate = DateTime.Now;
            }

            if (NewProject.TargetAmount == null)
            {
                var defaultPreset = AmountPresets.FirstOrDefault();
                if (defaultPreset != null)
                {
                    NewProject.TargetAmount = new MutableAmountUI { Sats = defaultPreset.Sats };
                }
            }
        }

        /// <summary>
        /// Helper property to bridge between preset selection (long) and TargetAmount (IAmountUI)
        /// </summary>
        public long? SelectedPresetSats
        {
            get => NewProject.TargetAmount?.Sats;
            set
            {
                if (value.HasValue)
                {
                    if (NewProject.TargetAmount == null)
                        NewProject.TargetAmount = new MutableAmountUI { Sats = value.Value };
                    else if (NewProject.TargetAmount is MutableAmountUI mutable)
                        mutable.Sats = value.Value;
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

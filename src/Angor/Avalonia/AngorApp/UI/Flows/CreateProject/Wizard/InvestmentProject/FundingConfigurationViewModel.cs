using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public interface IFundingConfigurationViewModel
    {
        IEnumerable<IAmountUI> AmountPresets { get; }
    }

    public class FundingConfigurationViewModelSample : IFundingConfigurationViewModel
    {
        public IEnumerable<IAmountUI> AmountPresets { get; } =
        [
            AmountUI.FromBtc(0.25),
            AmountUI.FromBtc(0.5),
            AmountUI.FromBtc(1),
            AmountUI.FromBtc(2.5),
        ];
    }

    public class FundingConfigurationViewModel : IHaveTitle, IFundingConfigurationViewModel
    {
        public NewProject NewProject { get; }

        public FundingConfigurationViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Funding Configuration");

        public IEnumerable<IAmountUI> AmountPresets { get; } =
        [
            AmountUI.FromBtc(0.25),
            AmountUI.FromBtc(0.5),
            AmountUI.FromBtc(1),
            AmountUI.FromBtc(2.5),
        ];
    }
}
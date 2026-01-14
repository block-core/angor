using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class FundingConfigurationViewModelSample : IFundingConfigurationViewModel
    {
        public IEnumerable<IAmountUI> AmountPresets { get; } =
        [
            AmountUI.FromBtc(0.25),
            AmountUI.FromBtc(0.5),
            AmountUI.FromBtc(1),
            AmountUI.FromBtc(2.5),
        ];

        public IInvestmentProjectConfig NewProject { get; } = new InvestmentProjectConfigSample();
        public long? SelectedPresetSats { get; set; }
    }
}

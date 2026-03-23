using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public interface IFundingConfigurationViewModel
    {
        IEnumerable<IAmountUI> AmountPresets { get; }
        IInvestmentProjectConfig NewProject { get; }
        long? SelectedPresetSats { get; set; }
        int MinPenaltyDays { get; }
        int MaxPenaltyDays { get; }
    }
}

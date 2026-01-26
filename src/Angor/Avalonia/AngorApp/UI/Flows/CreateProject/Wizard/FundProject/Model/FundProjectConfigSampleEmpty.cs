using System.Collections.ObjectModel;
using Avalonia.Controls.Selection;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public partial class FundProjectConfigSampleEmpty : ReactiveValidationObject, IFundProjectConfig
    {
        [Reactive] private PayoutFrequency? payoutFrequency;

        public IObservable<bool> IsValid { get; } = Observable.Return(false);
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public IAmountUI? GoalAmount { get; set; }
        public string AvatarUri { get; set; } = string.Empty;
        public string BannerUri { get; set; } = string.Empty;
        public string Nip05 { get; set; } = string.Empty;
        public string Lud16 { get; set; } = string.Empty;
        public string Nip57 { get; set; } = string.Empty;
        public ReactiveSelection<int, int> SelectedInstallments { get; } = new(new SelectionModel<int>() { SingleSelect = false, }, i => i);
        public int? MonthlyPayoutDate { get; set; }
        public DayOfWeek? WeeklyPayoutDay { get; set; }
        public IEnumerable<int> AvailableInstallmentCounts { get; } = [3, 6, 12];
        public IAmountUI? Threshold { get; set; } = AmountUI.FromBtc(0.01);
    }
}
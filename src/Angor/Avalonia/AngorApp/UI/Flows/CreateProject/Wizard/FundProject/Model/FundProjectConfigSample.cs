using System.Linq;
using AngorApp.Model.Amounts;
using Avalonia.Controls.Selection;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public class FundProjectConfigSample : ReactiveValidationObject, IFundProjectConfig
    {
        public string Name { get; set; } = "Sample Fund Project";
        public string Description { get; set; } = "This is a sample fund project for design-time preview";
        public string Website { get; set; } = "https://example.com";
        public IAmountUI? GoalAmount { get; set; } = AmountUI.FromBtc(1.0);

        public string AvatarUri { get; set; } = string.Empty;
        public string BannerUri { get; set; } = string.Empty;
        public string Nip05 { get; set; } = string.Empty;
        public string Lud16 { get; set; } = string.Empty;
        public string Nip57 { get; set; } = string.Empty;

        public PayoutFrequency? PayoutFrequency { get; set; } = Model.PayoutFrequency.Monthly;
        public ReactiveSelection<int, int> SelectedInstallments { get; set; } = new(new SelectionModel<int>([3, 6, 12]), i => i);
        public int? MonthlyPayoutDate { get; set; } = 15;
        public DayOfWeek? WeeklyPayoutDay { get; set; } = DayOfWeek.Monday;
        public IEnumerable<int> AvailableInstallmentCounts { get; } = [3, 6, 12];
        public IAmountUI? Threshold { get; set; } = AmountUI.FromBtc(0.001);
        public IObservable<bool> IsValid => Observable.Return(true);
    }

    public class PayoutConfigSample : IPayoutConfig
    {
        public decimal? Percent { get; set; }
        public DateTime? PayoutDate { get; set; }
        public IObservable<bool> IsValid => Observable.Return(true);
        public ReactiveUI.Validation.Contexts.IValidationContext ValidationContext => throw new NotImplementedException();
        public bool HasErrors => false;
        public System.Collections.IEnumerable GetErrors(string? propertyName) => Enumerable.Empty<string>();
        public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;
    }
}

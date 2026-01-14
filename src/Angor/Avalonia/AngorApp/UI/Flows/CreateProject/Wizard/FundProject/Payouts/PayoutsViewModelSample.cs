using System.Collections.Generic;
using System.Collections.ObjectModel;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Payouts
{
    public class PayoutsViewModelSample : IPayoutsViewModel
    {
        public IFundProjectConfig FundProject { get; set; } = new FundProjectConfigSample();
        public ReactiveCommand<Unit, Unit> GeneratePayouts { get; } = ReactiveCommand.Create(() => { });
        public ReactiveCommand<Unit, Unit> ClearPayouts { get; } = ReactiveCommand.Create(() => { });
        public IEnumerable<PayoutFrequency> AvailableFrequencies { get; } = new[] { PayoutFrequency.Monthly, PayoutFrequency.Weekly };
        public IEnumerable<int> AvailableInstallmentCounts { get; } = new[] { 3, 6, 9 };
        public IEnumerable<int> AvailablePayoutDates { get; } = new[] { 1, 15, 25 };
        public IEnumerable<DayOfWeek> AvailableDaysOfWeek { get; } = new[] { DayOfWeek.Monday, DayOfWeek.Friday };
        public ReadOnlyObservableCollection<IPayoutConfig> Payouts { get; } = new(new ObservableCollection<IPayoutConfig>
        {
            new PayoutConfigSample { PayoutDate = DateTime.Now.AddMonths(1), Percent = 0.33m },
            new PayoutConfigSample { PayoutDate = DateTime.Now.AddMonths(2), Percent = 0.33m },
            new PayoutConfigSample { PayoutDate = DateTime.Now.AddMonths(3), Percent = 0.34m }
        });
        public Zafiro.Avalonia.Misc.ReactiveSelection<int, int> InstallmentSelection { get; }

        public PayoutsViewModelSample()
        {
            InstallmentSelection = new Zafiro.Avalonia.Misc.ReactiveSelection<int, int>(
                new Avalonia.Controls.Selection.SelectionModel<int> { SingleSelect = false },
                x => x,
                _ => true
            );
        }

        public IObservable<string> Title => Observable.Return("Payouts");
        public IObservable<bool> IsValid => Observable.Return(true);
    }
}

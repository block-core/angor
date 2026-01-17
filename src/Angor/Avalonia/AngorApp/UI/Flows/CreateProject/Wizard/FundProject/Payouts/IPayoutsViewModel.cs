using System.Collections.Generic;
using System.Collections.ObjectModel;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Payouts
{
    public interface IPayoutsViewModel : IHaveTitle, IValidatable
    {
        IFundProjectConfig FundProject { get; }
        ReactiveCommand<Unit, Unit> GeneratePayouts { get; }
        ReactiveCommand<Unit, Unit> ClearPayouts { get; }
        ReadOnlyObservableCollection<IPayoutConfig> Payouts { get; }
        IEnumerable<PayoutFrequency> AvailableFrequencies { get; }
        IEnumerable<int> AvailableInstallmentCounts { get; }
        IEnumerable<int> AvailablePayoutDates { get; }
        IEnumerable<DayOfWeek> AvailableDaysOfWeek { get; }
    }
}

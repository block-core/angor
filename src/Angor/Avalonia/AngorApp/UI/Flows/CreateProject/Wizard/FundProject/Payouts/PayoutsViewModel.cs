using System.Linq;
using System.Reactive.Disposables;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Helpers;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;
using Avalonia.Controls.Selection;
using DynamicData;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Payouts
{
    public class FundPayoutsViewModel : ReactiveObject, IHaveTitle, IPayoutsViewModel, IDisposable, IValidatable
    {
        public IFundProjectConfig FundProject { get; }
        private readonly ReadOnlyObservableCollection<IPayoutConfig> payouts;
        private readonly SourceList<IPayoutConfig> payoutsSource = new();
        private readonly CompositeDisposable disposables = new();

        public FundPayoutsViewModel(IFundProjectConfig fundProject)
        {
            FundProject = fundProject;

            payoutsSource.Connect()
                .Bind(out payouts)
                .Subscribe()
                .DisposeWith(disposables);

            var canGeneratePayouts = this.WhenAnyValue(
                model => model.FundProject.PayoutFrequency,
                model => model.FundProject.MonthlyPayoutDate,
                model => model.FundProject.WeeklyPayoutDay,
                mod => mod.FundProject.SelectedInstallments.SelectedItems.Count,
                (frequency, monthDate, dayOfWeek, installmentCount) =>
                {
                    if (installmentCount == 0)
                        return false;
                    if (frequency == null)
                        return false;
                    if (frequency == PayoutFrequency.Monthly && monthDate == null)
                        return false;
                    if (frequency == PayoutFrequency.Weekly && dayOfWeek == null)
                        return false;
                    return true;
                })
                .ObserveOn(RxApp.MainThreadScheduler);

            GeneratePayouts = ReactiveCommand.Create(DoGeneratePayouts, canGeneratePayouts);
            ClearPayouts = ReactiveCommand.Create(() => payoutsSource.Clear());
        }

        private void DoGeneratePayouts()
        {
            if (FundProject.PayoutFrequency == null)
                return;
            var maxInstallments = FundProject.SelectedInstallments.SelectedItems.DefaultIfEmpty(0).Max();
            if (maxInstallments == 0)
                return;

            var generated = PayoutGenerator.Generate(
                FundProject.PayoutFrequency.Value,
                maxInstallments,
                DateTime.Now,
                FundProject.MonthlyPayoutDate,
                FundProject.WeeklyPayoutDay
            );

            payoutsSource.Edit(list =>
            {
                list.Clear();
                list.AddRange(generated);
            });
        }

        public IEnumerable<PayoutFrequency> AvailableFrequencies { get; } = System.Enum.GetValues<PayoutFrequency>();
        public IEnumerable<int> AvailableInstallmentCounts => FundProject.AvailableInstallmentCounts;
        public IEnumerable<int> AvailablePayoutDates { get; } = Enumerable.Range(1, 29).ToList();
        public IEnumerable<DayOfWeek> AvailableDaysOfWeek { get; } = System.Enum.GetValues<DayOfWeek>();

        public ReactiveCommand<Unit, Unit> GeneratePayouts { get; }
        public ReactiveCommand<Unit, Unit> ClearPayouts { get; }
        public ReadOnlyObservableCollection<IPayoutConfig> Payouts => payouts;

        public IObservable<string> Title => Observable.Return("Payouts");

        public void Dispose()
        {
            disposables.Dispose();
        }

        public IObservable<bool> IsValid => Observable.Return(true);
    }
}

using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls.Selection;
using DynamicData;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using ReactiveUI;
using Zafiro.Avalonia.Misc;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model
{
    public partial class FundProjectConfig : ReactiveValidationObject, IFundProjectConfig, IDisposable
    {
        protected readonly CompositeDisposable Disposables = new();

        [Reactive] private string name = string.Empty;
        [Reactive] private string description = string.Empty;
        [Reactive] private string website = string.Empty;


        [Reactive] private IAmountUI? goalAmount;
        [Reactive] private IAmountUI? threshold;

        [Reactive] private string avatarUri = DebugData.GetDefaultImageUriString(170, 170);
        [Reactive] private string bannerUri = DebugData.GetDefaultImageUriString(820, 312);
        
        [Reactive] private string nip05 = string.Empty;
        [Reactive] private string lud16 = string.Empty;
        [Reactive] private string nip57 = string.Empty;


        [Reactive] private PayoutFrequency? payoutFrequency;
        [Reactive] private int? monthlyPayoutDate;
        [Reactive] private DayOfWeek? weeklyPayoutDay;

        public FundProjectConfig()
        {
            SelectionModel<int> selectionModel = new(AvailableInstallmentCounts) { SingleSelect = false };
            SelectedInstallments = new ReactiveSelection<int, int>(selectionModel, i => i)
                .DisposeWith(Disposables);


            this.ValidationRule(x => x.Name, x => !string.IsNullOrWhiteSpace(x), "Project name is required.")
                .DisposeWith(Disposables);
            this.ValidationRule(
                    x => x.Description,
                    x => !string.IsNullOrWhiteSpace(x),
                    "Project description is required.")
                .DisposeWith(Disposables);
            this.ValidationRule(
                    x => x.Website,
                    x => string.IsNullOrWhiteSpace(x) || (Uri.TryCreate(x, UriKind.Absolute, out var uri) &&
                                                          (uri.Scheme == Uri.UriSchemeHttp ||
                                                           uri.Scheme == Uri.UriSchemeHttps)),
                    "Website must be a valid URL (http or https).")
                .DisposeWith(Disposables);


            this.ValidationRule(
                    this.WhenAnyValue(
                        x => x.GoalAmount,
                        x => x.GoalAmount!.Sats,
                        (amount, sats) => amount != null && sats > 0),
                    isValid => isValid,
                    _ => "Goal amount must be greater than 0.")
                .DisposeWith(Disposables);
            this.ValidationRule(x => x.GoalAmount, x => x != null, _ => "Goal amount is required.")
                .DisposeWith(Disposables);


            this.ValidationRule(x => x.PayoutFrequency, x => x != null, "Payout frequency is required.")
                .DisposeWith(Disposables);
            this.ValidationRule(
                    x => x.SelectedInstallments,
                    SelectedInstallments.SelectionCount,
                    i => i > 0,
                    _ => "At least one installment count is required.")
                .DisposeWith(Disposables);


            var monthlyDateValid = Observable.CombineLatest(
                this.WhenAnyValue(x => x.PayoutFrequency),
                this.WhenAnyValue(x => x.MonthlyPayoutDate),
                (freq, date) =>
                    freq != AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model.PayoutFrequency.Monthly ||
                    date != null);
            this.ValidationRule(
                    x => x.MonthlyPayoutDate,
                    monthlyDateValid,
                    "Monthly payout date is required for monthly frequency.")
                .DisposeWith(Disposables);

            this.ValidationRule(
                    x => x.MonthlyPayoutDate,
                    x => x is null || (x >= 1 && x <= 29),
                    "Monthly payout date must be between 1 and 29.")
                .DisposeWith(Disposables);


            var weeklyDayValid = Observable.CombineLatest(
                this.WhenAnyValue(x => x.PayoutFrequency),
                this.WhenAnyValue(x => x.WeeklyPayoutDay),
                (freq, day) =>
                    freq != AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model.PayoutFrequency.Weekly ||
                    day != null);
            this.ValidationRule(
                    x => x.WeeklyPayoutDay,
                    weeklyDayValid,
                    "Weekly payout day is required for weekly frequency.")
                .DisposeWith(Disposables);
        }

        public ReactiveSelection<int, int> SelectedInstallments { get; }

        public IEnumerable<int> AvailableInstallmentCounts { get; } = [3, 6, 12];

        public new void Dispose()
        {
            Disposables.Dispose();

            base.Dispose();
        }

        public IObservable<bool> IsValid => this.IsValid();
    }
}
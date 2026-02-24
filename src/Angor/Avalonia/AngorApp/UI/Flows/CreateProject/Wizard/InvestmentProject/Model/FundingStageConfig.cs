using System.Reactive.Disposables;
using System.Reactive.Subjects;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using System.Globalization;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model
{
    public partial class FundingStageConfig : ReactiveValidationObject, IFundingStageConfig
    {
        private readonly CompositeDisposable disposable = new();
        private readonly BehaviorSubject<IObservable<DateTime>> previousDateSource;
        [ObservableAsProperty] private TimeSpan? timeFromPrevious;

        public FundingStageConfig(ValidationEnvironment environment = ValidationEnvironment.Production)
        {
            var minDaysAfterPrevious = environment == ValidationEnvironment.Debug ? 0 : 1;

            previousDateSource = new BehaviorSubject<IObservable<DateTime>>(Observable.Return(DateTime.MinValue));
            var previousDate = previousDateSource.Switch();
            var minAllowed = previousDate.Select(d => d.AddDays(minDaysAfterPrevious));

            timeFromPreviousHelper = this.WhenAnyValue(x => x.ReleaseDate)
                                         .CombineLatest(minAllowed, (relDate, minDate) => new { relDate, minDate })
                                         .Select(x => x.relDate != null ? x.relDate.Value.Date - x.minDate.Date.AddDays(-1) : (TimeSpan?)null)
                                         .ToProperty(this, x => x.TimeFromPrevious)
                                         .DisposeWith(disposable);

            this.ValidationRule(stage => stage.ReleaseDate, time => time != null, "Release date is required").DisposeWith(disposable);
            this.ValidationRule(stage => stage.ReleaseDate,
                this.WhenAnyValue(x => x.ReleaseDate)
                    .CombineLatest(minAllowed, (relDate, minDate) => new { relDate, minDate }),
                arg => arg.relDate == null || arg.relDate.Value.Date >= arg.minDate,
                arg => $"Date must be {arg.minDate.Date:d} or later").DisposeWith(disposable);

            this.ValidationRule(x => x.Percent, x => x != null, "Percent is required").DisposeWith(disposable);
        }

        private decimal? percent;
        [Reactive] private DateTime? releaseDate;

        public decimal? Percent
        {
            get => percent;
            set => this.RaiseAndSetIfChanged(ref percent, value.HasValue ? Math.Clamp(value.Value, 0.0m, 1.0m) : value);
        }

        public void SetPreviousDateSource(IObservable<DateTime> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            previousDateSource.OnNext(source);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                previousDateSource.Dispose();
                disposable.Dispose();
            }
        }

        public IObservable<bool> IsValid => this.IsValid();
    }
}

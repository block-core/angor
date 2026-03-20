using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public partial class StagesViewModel : ReactiveObject, IHaveTitle, IStagesViewModel, IDisposable, IValidatable
    {
        private static readonly PeriodUnit DefaultDurationUnit = PeriodUnit.Months;
        private static readonly IReadOnlyList<PeriodOption> DurationUnitOptions =
        [
            new() { Title = "Months", Unit = PeriodUnit.Months, Value = 1 },
            new() { Title = "Weeks", Unit = PeriodUnit.Weeks, Value = 1 },
            new() { Title = "Days", Unit = PeriodUnit.Days, Value = 1 }
        ];
        private static readonly IReadOnlyDictionary<PeriodUnit, IReadOnlyList<PeriodOption>> DurationPresetOptionsByUnit =
            new Dictionary<PeriodUnit, IReadOnlyList<PeriodOption>>
            {
                [PeriodUnit.Days] = CreatePresetOptions(PeriodUnit.Days, "Days", 3, 7, 14, 21, 28, 30),
                [PeriodUnit.Weeks] = CreatePresetOptions(PeriodUnit.Weeks, "Weeks", 2, 4, 6, 8, 12),
                [PeriodUnit.Months] = CreatePresetOptions(PeriodUnit.Months, "Months", 3, 6, 12, 18, 24)
            };
        private static readonly IReadOnlyList<PeriodOption> ReleaseFrequencyOptions =
        [
            new() { Title = "Weekly", Unit = PeriodUnit.Weeks, Value = 1 },
            new() { Title = "Monthly", Unit = PeriodUnit.Months, Value = 1 },
            new() { Title = "Bi-Monthly", Unit = PeriodUnit.Months, Value = 2 },
            new() { Title = "Quarterly", Unit = PeriodUnit.Months, Value = 3 }
        ];

        public IInvestmentProjectConfig NewProject { get; }
        [Reactive]
        private bool isAdvanced;
        [Reactive]
        private int? selectedDurationValue;
        [Reactive]
        private PeriodUnit? selectedDurationUnit;
        [Reactive]
        private PeriodOption? selectedLength;
        [Reactive]
        private PeriodOption? releaseFrequency;
        private readonly CompositeDisposable disposables = new();

        public StagesViewModel(IInvestmentProjectConfig newProject)
        {
            NewProject = newProject;
            SelectedDurationUnit = DefaultDurationUnit;

            AddStage = ReactiveCommand.Create(DoAddStage);
            RemoveStage = ReactiveCommand.Create<IFundingStageConfig>(DoRemoveStage);
            GenerateStages = ReactiveCommand.Create(DoGenerateStages);
            ClearStages = ReactiveCommand.Create(DoClearStages);

            SyncDuration().DisposeWith(disposables);
            SyncSelectedLength().DisposeWith(disposables);

            var errorSummary = new ErrorSummarizer(newProject.ValidationContext).DisposeWith(disposables);
            Errors = errorSummary.Errors;
        }

        private IDisposable SyncSelectedLength()
        {
            return this.WhenAnyValue(x => x.SelectedDurationValue, x => x.SelectedDurationUnit, CreateSelectedLength)
                       .DistinctUntilChanged()
                       .Subscribe(length => SelectedLength = length);
        }

        private IDisposable SyncDuration()
        {
            return this.WhenAnyValue(x => x.SelectedLength)
                       .DistinctUntilChanged()
                       .Where(x => x is not null)
                       .Subscribe(length =>
                       {
                           SelectedDurationUnit = length!.Unit;
                           SelectedDurationValue = length.Value;
                       });
        }

        public IReadOnlyList<PeriodOption> DurationUnits => DurationUnitOptions;
        public IObservable<IReadOnlyList<PeriodOption>> DurationPresets =>
            this.WhenAnyValue(x => x.SelectedDurationUnit)
                .Select(durationUnit => durationUnit is { } value &&
                                        DurationPresetOptionsByUnit.TryGetValue(value, out var presets)
                    ? presets
                    : Array.Empty<PeriodOption>());

        public IReadOnlyList<PeriodOption> ReleaseFrequencies => ReleaseFrequencyOptions;

        private static IReadOnlyList<PeriodOption> CreatePresetOptions(PeriodUnit unit, string title, params int[] values)
        {
            return values
                .Select(value => new PeriodOption
                {
                    Value = value,
                    Title = $"{value} {title}",
                    Unit = unit
                })
                .ToArray();
        }

        private static PeriodOption? CreateSelectedLength(int? value, PeriodUnit? unit)
        {
            if (value is null || unit is null)
            {
                return null;
            }

            return new PeriodOption
            {
                Value = value.Value,
                Unit = unit.Value,
                Title = $"{value.Value} {GetUnitTitle(unit.Value)}"
            };
        }

        private static string GetUnitTitle(PeriodUnit unit)
        {
            return unit switch
            {
                PeriodUnit.Days => "Days",
                PeriodUnit.Weeks => "Weeks",
                PeriodUnit.Months => "Months",
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }

        private void DoRemoveStage(IFundingStageConfig stage)
        {
            NewProject.RemoveStage(stage);
        }

        private void DoAddStage()
        {
            NewProject.AddStage();
        }

        private void DoGenerateStages()
        {
            if (!TryGetGenerationSettings(out var startDate, out var endDate, out var frequency))
            {
                return;
            }

            ClearStagesInternal();

            var releaseDates = GetReleaseDates(startDate, endDate, frequency);
            var stageCount = releaseDates.Count;
            var stagePercents = GetStagePercents(stageCount);

            for (var i = 0; i < stageCount; i++)
            {
                NewProject.CreateAndAddStage(stagePercents[i], releaseDates[i]);
            }
        }

        private bool TryGetGenerationSettings(out DateTime startDate, out DateTime endDate, out PeriodOption frequency)
        {
            startDate = default;
            endDate = default;
            frequency = default!;

            if (!NewProject.FundingEndDate.HasValue)
            {
                return false;
            }

            if (SelectedDurationValue is null || SelectedDurationValue <= 0)
            {
                return false;
            }

            if (!SelectedDurationUnit.HasValue)
            {
                return false;
            }

            if (ReleaseFrequency is null || ReleaseFrequency.Value <= 0)
            {
                return false;
            }

            startDate = NewProject.FundingEndDate.Value.Date;
            endDate = AddPeriod(startDate, SelectedDurationValue.Value, SelectedDurationUnit.Value);
            frequency = ReleaseFrequency;
            return true;
        }

        private static List<DateTime> GetReleaseDates(DateTime startDate, DateTime endDate, PeriodOption frequency)
        {
            var releaseDates = new List<DateTime>();
            var iteration = 1;

            while (true)
            {
                var nextDate = AddPeriod(startDate, frequency.Value * iteration, frequency.Unit);
                if (nextDate >= endDate)
                {
                    releaseDates.Add(endDate);
                    return releaseDates;
                }

                releaseDates.Add(nextDate);
                iteration++;
            }
        }

        private static DateTime AddPeriod(DateTime date, int value, PeriodUnit unit)
        {
            return unit switch
            {
                PeriodUnit.Days => date.AddDays(value),
                PeriodUnit.Weeks => date.AddDays(value * 7),
                PeriodUnit.Months => date.AddMonths(value),
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }

        private static decimal[] GetStagePercents(int stageCount)
        {
            var basePercent = 100 / stageCount;
            var remainder = 100 % stageCount;
            var percents = new decimal[stageCount];

            for (var i = 0; i < stageCount; i++)
            {
                // Example: 3 stages become 33%, 33%, 34% so the total stays at 100%.
                var wholePercent = basePercent + (i >= stageCount - remainder ? 1 : 0);
                percents[i] = wholePercent / 100m;
            }

            return percents;
        }

        private void DoClearStages()
        {
            ClearStagesInternal();
        }

        private void ClearStagesInternal()
        {
            var existingStages = new List<IFundingStageConfig>(NewProject.Stages);
            foreach (var stage in existingStages)
            {
                NewProject.RemoveStage(stage);
            }
        }

        public ReactiveCommand<Unit, Unit> AddStage { get; }
        public ReactiveCommand<IFundingStageConfig, Unit> RemoveStage { get; }
        public ReactiveCommand<Unit, Unit> GenerateStages { get; }
        public ReactiveCommand<Unit, Unit> ClearStages { get; }
        public ReadOnlyObservableCollection<string> Errors { get; }

        public IObservable<string> Title => Observable.Return("Stages");

        public void Dispose()
        {
            disposables.Dispose();
        }

        public IObservable<bool> IsValid => NewProject.WhenValid(x => x.Stages);
    }
}

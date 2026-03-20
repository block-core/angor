using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public partial class StagesViewModelSample : ReactiveObject, IStagesViewModel, IDisposable
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
        private readonly CompositeDisposable disposables = new();

        public IInvestmentProjectConfig NewProject { get; set; } = new InvestmentProjectConfigSample();
        [Reactive]
        private bool isAdvanced;
        [Reactive]
        private int? selectedDurationValue;
        [Reactive]
        private PeriodUnit? selectedDurationUnit = DefaultDurationUnit;
        [Reactive]
        private PeriodOption? selectedLength;
        [Reactive]
        private PeriodOption? releaseFrequency;

        public StagesViewModelSample()
        {
            this.WhenAnyValue(x => x.SelectedLength)
                .DistinctUntilChanged()
                .Where(x => x is not null)
                .Subscribe(length =>
                {
                    SelectedDurationUnit = length!.Unit;
                    SelectedDurationValue = length.Value;
                })
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.SelectedDurationValue, x => x.SelectedDurationUnit)
                .Select(tuple => CreateSelectedLength(tuple.Item1, tuple.Item2))
                .DistinctUntilChanged()
                .Subscribe(length => SelectedLength = length)
                .DisposeWith(disposables);
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

        public ReactiveCommand<Unit, Unit> AddStage { get; } = ReactiveCommand.Create(() => { });
        public ReactiveCommand<IFundingStageConfig, Unit> RemoveStage { get; } = ReactiveCommand.Create<IFundingStageConfig>(_ => { });
        public ReactiveCommand<Unit, Unit> GenerateStages { get; } = ReactiveCommand.Create(() => { });
        public ReactiveCommand<Unit, Unit> ClearStages { get; } = ReactiveCommand.Create(() => { });
        public ReadOnlyObservableCollection<string> Errors { get; } = new(new ObservableCollection<string>());

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}

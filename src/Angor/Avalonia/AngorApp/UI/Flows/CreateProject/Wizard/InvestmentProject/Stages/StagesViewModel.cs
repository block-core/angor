using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using System.Reactive;
using System.Reactive.Linq;
using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public partial class StagesViewModel : ReactiveObject, IHaveTitle, IStagesViewModel, IDisposable, IValidatable
    {
        private static readonly TimeSpan DefaultDurationUnit = TimeSpan.FromDays(30);

        public IInvestmentProjectConfig NewProject { get; }
        [Reactive]
        private bool isAdvanced;
        [Reactive]
        private int? durationValue;
        [Reactive]
        private TimeSpan? durationUnit;
        [Reactive]
        private TimeSpan? releaseFrequency;
        [Reactive]
        private int? durationPreset;
        private readonly CompositeDisposable disposables = new();

        public StagesViewModel(IInvestmentProjectConfig newProject)
        {
            NewProject = newProject;
            DurationUnit = DefaultDurationUnit;

            AddStage = ReactiveCommand.Create(DoAddStage);
            RemoveStage = ReactiveCommand.Create<IFundingStageConfig>(DoRemoveStage);
            GenerateStages = ReactiveCommand.Create(DoGenerateStages);
            ClearStages = ReactiveCommand.Create(DoClearStages);

            var presetValues = this.WhenAnyValue(x => x.DurationPreset)
                .WhereNotNull();

            presetValues
                .BindTo(this, x => x.DurationValue)
                .DisposeWith(disposables);

            presetValues
                .Select(_ => DefaultDurationUnit)
                .BindTo(this, x => x.DurationUnit)
                .DisposeWith(disposables);

            var errorSummary = new ErrorSummarizer(newProject.ValidationContext).DisposeWith(disposables);
            Errors = errorSummary.Errors;
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
            if (!TryGetGenerationSettings(out var startDate, out var totalDuration, out var frequency))
            {
                return;
            }

            ClearStagesInternal();

            var stageCount = CalculateStageCount(totalDuration, frequency);
            var percent = (decimal)1 / stageCount;

            for (var i = 1; i <= stageCount; i++)
            {
                var offsetTicks = frequency.Ticks * i;
                if (offsetTicks > totalDuration.Ticks)
                {
                    offsetTicks = totalDuration.Ticks;
                }

                var releaseDate = startDate.AddTicks(offsetTicks);
                NewProject.CreateAndAddStage(percent, releaseDate);
            }
        }

        private bool TryGetGenerationSettings(out DateTime startDate, out TimeSpan totalDuration, out TimeSpan frequency)
        {
            startDate = default;
            totalDuration = default;
            frequency = default;

            if (!NewProject.FundingEndDate.HasValue)
            {
                return false;
            }

            if (DurationValue is null || DurationValue <= 0)
            {
                return false;
            }

            if (!DurationUnit.HasValue || DurationUnit.Value <= TimeSpan.Zero)
            {
                return false;
            }

            if (!ReleaseFrequency.HasValue || ReleaseFrequency.Value <= TimeSpan.Zero)
            {
                return false;
            }

            totalDuration = TimeSpan.FromTicks(DurationUnit.Value.Ticks * DurationValue.Value);
            if (totalDuration <= TimeSpan.Zero)
            {
                return false;
            }

            frequency = ReleaseFrequency.Value;
            startDate = NewProject.FundingEndDate.Value.Date;
            return true;
        }

        private static int CalculateStageCount(TimeSpan totalDuration, TimeSpan frequency)
        {
            var count = (int)Math.Ceiling(totalDuration.Ticks / (double)frequency.Ticks);
            return Math.Max(1, count);
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

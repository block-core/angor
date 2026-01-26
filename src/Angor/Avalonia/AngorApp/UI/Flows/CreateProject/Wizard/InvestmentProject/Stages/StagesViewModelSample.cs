using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public partial class StagesViewModelSample : ReactiveObject, IStagesViewModel, IDisposable
    {
        private static readonly TimeSpan DefaultDurationUnit = TimeSpan.FromDays(30);
        private readonly CompositeDisposable disposables = new();

        public IInvestmentProjectConfig NewProject { get; set; } = new InvestmentProjectConfigSample();
        [Reactive]
        private bool isAdvanced;
        [Reactive]
        private int? durationValue;
        [Reactive]
        private TimeSpan? durationUnit = DefaultDurationUnit;
        [Reactive]
        private TimeSpan? releaseFrequency;
        [Reactive]
        private int? durationPreset;

        public StagesViewModelSample()
        {
            var presetValues = this.WhenAnyValue(x => x.DurationPreset)
                                   .WhereNotNull();

            presetValues
                .BindTo(this, x => x.DurationValue)
                .DisposeWith(disposables);

            presetValues
                .Select(_ => DefaultDurationUnit)
                .BindTo(this, x => x.DurationUnit)
                .DisposeWith(disposables);
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

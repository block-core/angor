using System.Collections.ObjectModel;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public interface IStagesViewModel
    {
        IInvestmentProjectConfig NewProject { get; }

        int? SelectedDurationValue { get; set; }
        PeriodUnit? SelectedDurationUnit { get; set; }
        PeriodOption? SelectedLength { get; set; }
        PeriodOption? ReleaseFrequency { get; set; }
        IReadOnlyList<PeriodOption> DurationUnits { get; }
        IObservable<IReadOnlyList<PeriodOption>> DurationPresets { get; }
        IReadOnlyList<PeriodOption> ReleaseFrequencies { get; }

        public void ToggleEditor()
        {
            IsAdvanced = !IsAdvanced;
        }

        public bool IsAdvanced { get; set; }

        ReactiveCommand<Unit, Unit> AddStage { get; }
        ReactiveCommand<IFundingStageConfig, Unit> RemoveStage { get; }
        ReactiveCommand<Unit, Unit> GenerateStages { get; }
        ReactiveCommand<Unit, Unit> ClearStages { get; }
        ReadOnlyObservableCollection<string> Errors { get; }
    }
}

using System.Collections.ObjectModel;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public interface IStagesViewModel
    {
        IInvestmentProjectConfig NewProject { get; }

        int? DurationValue { get; set; }
        TimeSpan? DurationUnit { get; set; }
        int? DurationPreset { get; set; }
        TimeSpan? ReleaseFrequency { get; set; }

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

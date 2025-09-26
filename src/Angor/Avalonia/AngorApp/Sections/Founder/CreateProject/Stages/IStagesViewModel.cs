using System;
using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.Stages;

public interface IStagesViewModel : IHaveErrors
{
    IEnhancedCommand AddStage { get; }
    ICollection<ICreateProjectStage> Stages { get; }
    IObservable<bool> IsValid { get; }
    IObservable<DateTime?> LastStageDate { get; }
    public IStagesCreatorViewModel StagesCreator { get; }
    public IEnhancedCommand CreateStages { get; }
}
using System;
using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Sections.Founder.CreateProject.Stages.Creator;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.CreateProject.Stages;

public interface IStagesViewModel : IHaveErrors
{
    IEnhancedCommand AddStage { get; }
    ICollection<ICreateProjectStage> Stages { get; }
    IObservable<bool> IsValid { get; }
    IObservable<DateTime?> LastStageDate { get; }
    public IStagesCreatorViewModel StagesCreator { get; }
    public IEnhancedCommand CreateStages { get; }
}
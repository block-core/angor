using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Sections.Founder.CreateProject.Profile;
using AngorApp.UI.Sections.Founder.CreateProject.Stages;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.CreateProject;

public interface ICreateProjectViewModel
{
    IEnhancedCommand<Result<string>> Create { get; }
    public IStagesViewModel StagesViewModel { get; }
    public IProfileViewModel ProfileViewModel { get; }
    public IFundingStructureViewModel FundingStructureViewModel { get; }
}
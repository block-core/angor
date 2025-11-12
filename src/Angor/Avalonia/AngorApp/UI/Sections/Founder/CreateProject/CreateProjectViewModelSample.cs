using AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.UI.Sections.Founder.CreateProject.Profile;
using AngorApp.UI.Sections.Founder.CreateProject.Stages;
using Zafiro.UI.Commands;

namespace AngorApp.UI.Sections.Founder.CreateProject;

public class CreateProjectViewModelSample : ICreateProjectViewModel
{
    public IEnhancedCommand<Result<string>> Create { get; }
    public IStagesViewModel StagesViewModel { get; set; }
    public IProfileViewModel ProfileViewModel { get; set; }
    public IFundingStructureViewModel FundingStructureViewModel { get; set; }
}
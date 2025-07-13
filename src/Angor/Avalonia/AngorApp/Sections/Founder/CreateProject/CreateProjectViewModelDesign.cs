using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public class CreateProjectViewModelDesign : ICreateProjectViewModel
{
    public IEnhancedCommand Create { get; }
    public IStagesViewModel StagesViewModel { get; set; }
    public IProfileViewModel ProfileViewModel { get; set; }
    public IFundingStructureViewModel FundingStructureViewModel { get; set; }
}
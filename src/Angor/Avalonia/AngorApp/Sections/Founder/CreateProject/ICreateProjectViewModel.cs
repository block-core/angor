using AngorApp.Sections.Founder.CreateProject.FundingStructure;
using AngorApp.Sections.Founder.CreateProject.Profile;
using AngorApp.Sections.Founder.CreateProject.Stages;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject;

public interface ICreateProjectViewModel
{
    IEnhancedCommand Create { get; }
    public IStagesViewModel StagesViewModel { get; }
    public IProfileViewModel ProfileViewModel { get; }
    public IFundingStructureViewModel FundingStructureViewModel { get; }
}
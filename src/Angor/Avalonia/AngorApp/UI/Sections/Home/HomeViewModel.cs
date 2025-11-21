using AngorApp.UI.Shell;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Home;

[Section("Home", icon: "fa-home")]
public class HomeSectionSectionViewModel(IShellViewModel shellViewModel, ICreateProjectFlow createProjectFlow) : IHomeSectionViewModel
{
    public IEnhancedCommand FindProjects { get; set; } = ReactiveCommand.Create(() => shellViewModel.SetSection("Find Projects")).Enhance();
    public IEnhancedCommand CreateProject { get; set; } = ReactiveCommand.Create(createProjectFlow.CreateProject).Enhance();
}
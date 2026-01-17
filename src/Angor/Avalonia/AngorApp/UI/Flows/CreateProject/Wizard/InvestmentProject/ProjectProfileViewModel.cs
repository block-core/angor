using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectProfileViewModel(IProjectProfile newProject) : IHaveTitle, IValidatable
    {
        public IProjectProfile NewProject { get; } = newProject;

        public IObservable<string> Title => Observable.Return("Project Profile");
        public IObservable<bool> IsValid => NewProject.WhenValid(
            x => x.Name,
            x => x.Description,
            x => x.Website);
    }
}
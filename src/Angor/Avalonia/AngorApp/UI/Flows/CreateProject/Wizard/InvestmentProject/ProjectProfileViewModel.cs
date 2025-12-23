using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectProfileViewModel : IHaveTitle
    {
        public NewProject NewProject { get; }

        public ProjectProfileViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Project Profile");
    }
}
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectImagesViewModel : IHaveTitle
    {
        public NewProject NewProject { get; }

        public ProjectImagesViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Project Images");
    }
}
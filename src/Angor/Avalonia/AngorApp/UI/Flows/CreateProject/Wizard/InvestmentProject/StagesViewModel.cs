using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class StagesViewModel : IHaveTitle
    {
        public NewProject NewProject { get; }

        public StagesViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Stages");
    }
}
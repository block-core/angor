using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ReviewAndDeployViewModel : IHaveTitle
    {
        public NewProject NewProject { get; }

        public ReviewAndDeployViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Review & Deploy");
    }
}
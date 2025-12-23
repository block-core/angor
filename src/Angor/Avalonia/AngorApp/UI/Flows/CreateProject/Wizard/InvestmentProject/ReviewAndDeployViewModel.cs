using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public interface IReviewAndDeployViewModel
    {
        INewProject NewProject { get; }
    }

    public class ReviewAndDeployViewModel : IHaveTitle, IReviewAndDeployViewModel
    {
        public INewProject NewProject { get; }

        public ReviewAndDeployViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Review & Deploy");
    }
}
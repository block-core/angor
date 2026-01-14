using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectImagesViewModel(IInvestmentProjectConfig newProject) : IHaveTitle
    {
        public IInvestmentProjectConfig NewProject { get; } = newProject;

        public IObservable<string> Title => Observable.Return("Project Images");
    }
}
namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectImagesViewModel(IProjectProfile newProject) : IHaveTitle
    {
        public IProjectProfile NewProject { get; } = newProject;

        public IObservable<string> Title => Observable.Return("Project Images");
    }
}
namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectImagesViewModelSample : IProjectTypeViewModel
    {
        public IObservable<string> Title => Observable.Return("Title");
        public bool IsStarted { get; set; }
    }
}
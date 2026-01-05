using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages
{
    public interface IStagesViewModel
    {
        INewProject NewProject { get; }

        public void ToggleEditor()
        {
            IsAdvanced = !IsAdvanced;
        }

        public bool IsAdvanced { get; set; }
    }

    public partial class StagesViewModelSample : ReactiveObject, IStagesViewModel
    {
        public INewProject NewProject { get; set; } = new NewProjectSample();
        [Reactive]
        private bool isAdvanced;
    }

    public partial class StagesViewModel : ReactiveObject, IHaveTitle, IStagesViewModel
    {
        public INewProject NewProject { get; }
        [Reactive]
        private bool isAdvanced;

        public StagesViewModel(NewProject newProject)
        {
            NewProject = newProject;
        }

        public IObservable<string> Title => Observable.Return("Stages");
    }
}
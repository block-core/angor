using System.Windows.Input;
using AngorApp.UI.Shared;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectProfileViewModel : IHaveTitle, IValidatable
    {
        public ProjectProfileViewModel(IProjectProfile newProject, Action? prefillAction = null)
        {
            NewProject = newProject;

            if (prefillAction is not null)
            {
                PrefillDebugData = ReactiveCommand.Create(() => prefillAction());
            }
        }

        public IProjectProfile NewProject { get; }
        public ICommand? PrefillDebugData { get; }

        public IObservable<string> Title => Observable.Return("Project Profile");
        public IObservable<bool> IsValid => NewProject.WhenValid(
            x => x.Name,
            x => x.Description,
            x => x.Website);
    }
}
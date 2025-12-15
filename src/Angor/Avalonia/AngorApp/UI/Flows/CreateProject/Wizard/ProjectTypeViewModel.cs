using System.Security.Principal;

namespace AngorApp.UI.Flows.CreateProject.Wizard
{
    public partial class ProjectTypeViewModel : ReactiveObject, IHaveTitle
    {
        [Reactive] private ProjectType projectType;

        public IObservable<string> Title => Observable.Return("Project Type");

        public IEnumerable<ProjectType> ProjectTypes { get; } =
        [
            new("Investment", "One-time funding with start and end dates.", new Icon("fa-arrow-trend-up")),
            new("Fund", "Recurring funding with periodic contributions.", new Icon("fa-bitcoin")),
            new("Subscription", "Ongoing subscription-based funding model.", new Icon("fa-arrows-rotate"))
        ];
    }

    public record ProjectType(string Name, string Description, object Icon);
}
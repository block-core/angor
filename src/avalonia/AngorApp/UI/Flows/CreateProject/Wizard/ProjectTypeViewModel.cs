using System.Linq;

namespace AngorApp.UI.Flows.CreateProject.Wizard
{
    public partial class ProjectTypeViewModel : ReactiveObject, IHaveTitle
    {
        [Reactive] private ProjectType projectType;

        public ProjectTypeViewModel()
        {
            ProjectType = ProjectTypes.First();
        }
        
        public IObservable<string> Title => Observable.Return("Project Type");

        public IEnumerable<ProjectType> ProjectTypes { get; } =
        [
            new("Investment", "I am looking for Investors", "Investors can fund during a funding period and then once the goal is met funds start being released in stages.", new Icon("fa-arrow-trend-up")),
            new("Fund", "I am looking for supporters", "Supporters can fund at anytime during the project timeline.", new Icon("fa-bitcoin")),
            new("Subscription", "I am looking for paid subscribers", "We offer weekly and monthly subscriptions paid for up front 3/6/12 months and released on a chosen day each week of month.", new Icon("fa-arrows-rotate"))
        ];
    }

    public record ProjectType(string Name, string Title, string Description, object Icon);
}
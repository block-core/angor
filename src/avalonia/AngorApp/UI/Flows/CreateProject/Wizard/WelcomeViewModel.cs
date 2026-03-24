namespace AngorApp.UI.Flows.CreateProject.Wizard
{
    public class WelcomeViewModel : IHaveTitle
    {
        public IEnhancedCommand<Result<Unit>> Start { get; } = EnhancedCommand.Create(() => Result.Success(Unit.Default));

        public IObservable<string> Title => Observable.Return("Welcome");
    };
}
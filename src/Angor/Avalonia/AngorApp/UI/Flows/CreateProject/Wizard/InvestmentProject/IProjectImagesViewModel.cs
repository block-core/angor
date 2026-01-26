namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public interface IProjectImagesViewModel
    {
        IEnhancedCommand<Maybe<Uri>> PickBanner { get; }
        IEnhancedCommand<Maybe<Uri>> PickAvatar { get; }
        IProjectProfile NewProject { get; }
    }
}
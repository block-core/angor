using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectImagesViewModelSample : IProjectImagesViewModel
    {
        public IEnhancedCommand<Maybe<Uri>> PickBanner { get; } = ReactiveCommand.Create(() => Maybe<Uri>.None).Enhance();
        public IEnhancedCommand<Maybe<Uri>> PickAvatar { get; } = ReactiveCommand.Create(() => Maybe<Uri>.None).Enhance();
        public IProjectProfile NewProject { get; } = new FundProjectConfigSample();
    }
}
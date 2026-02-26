using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;
using IStage = AngorApp.UI.Sections.Shared.ProjectV2.IStage;

namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public class InvestmentProject : Project, IInvestmentProject
    {
        public InvestmentProject(ProjectDto seed, IProjectAppService projectAppService) : base(seed)
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            Raised = refresh.Successes().Select(dto => new AmountUI(dto.TotalInvested));
            Target = new AmountUI(seed.TargetAmount);
            Refresh = refresh;
            Stages = Observable.Return(new List<IStage>());
        }

        public IAmountUI Target { get; }
        public IObservable<IAmountUI> Raised { get; }
        public IObservable<IEnumerable<IStage>> Stages { get; }
        public override IEnhancedCommand Refresh { get; }
    }
}
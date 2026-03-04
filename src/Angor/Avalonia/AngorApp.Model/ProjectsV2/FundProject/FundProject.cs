using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Shared.Models;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Model.ProjectsV2.FundProject
{
    public class FundProject : Project, IFundProject
    {
        public FundProject(ProjectDto seed, IProjectAppService projectAppService) : base(seed)
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            Funded = refresh.Successes().Select(dto => new AmountUI(dto.TotalInvested));
            FunderCount = refresh.Successes().Select(dto => dto.TotalInvestors ?? 0);
            Goal = new AmountUI(seed.TargetAmount);
            Refresh = refresh;
            Payments = refresh.Successes()
                .Select(dto => Payment.MapFrom(dto.DynamicStages ?? []))
                .StartWith([]);
            DynamicStagePatterns = seed.DynamicStagePatterns?.AsReadOnly() ?? new List<DynamicStagePattern>().AsReadOnly();

            ProjectStatus = refresh.Successes().Select(dto =>
            {
                var fundingFinished = DateTime.UtcNow.Date >= seed.FundingEndDate.Date;
                var reachedTarget = dto.TotalInvested >= seed.TargetAmount;
                return fundingFinished && !reachedTarget ? ProjectsV2.ProjectStatus.Closed : ProjectsV2.ProjectStatus.Open;
            }).StartWith(DateTime.UtcNow.Date >= seed.FundingEndDate.Date ? ProjectsV2.ProjectStatus.Closed : ProjectsV2.ProjectStatus.Open);
        }

        public IAmountUI Goal { get; }
        public IObservable<IAmountUI> Funded { get; }
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; }
        public IObservable<int> FunderCount { get; }
        public override IObservable<int> SupporterCount => FunderCount;
        public override IAmountUI FundingTarget => Goal;
        public override IObservable<IAmountUI> FundingRaised => Funded;
        public DateTimeOffset TransactionDate { get; set; }
        public FundingStatus Status { get; set; }
        public IReadOnlyList<DynamicStagePattern> DynamicStagePatterns { get; }
        public override IEnhancedCommand Refresh { get; }
        public override IObservable<ProjectStatus> ProjectStatus { get; }
    }
}
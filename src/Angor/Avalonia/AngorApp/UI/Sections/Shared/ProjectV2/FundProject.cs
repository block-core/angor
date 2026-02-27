using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Shared.ProjectV2
{
    public class FundProject : Project, IFundProject
    {
        public FundProject(ProjectDto seed, IProjectAppService projectAppService) : base(seed)
        {
            var refresh = EnhancedCommand.CreateWithResult(() => projectAppService.GetProjectStatistics(seed.Id));
            Funded = refresh.Successes().Select(dto => new AmountUI(dto.TotalInvested));
            Goal = new AmountUI(seed.TargetAmount);
            Refresh = refresh;
            Payments = Observable.Return(new List<IPayment>());
            
            FundingStart = seed.FundingStartDate;
            FundingEnd = seed.FundingEndDate;
        }
        
        public IAmountUI Goal { get; }
        public IObservable<IAmountUI> Funded { get; }
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; set; }
        public DateTimeOffset FundingStart { get; }
        public DateTimeOffset FundingEnd { get; init; }
        public DateTimeOffset TransactionDate { get; set; }
        public FundingStatus Status { get; set; }
        public override IEnhancedCommand Refresh { get; }
    }
}
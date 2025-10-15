using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using AngorApp.UI.Services;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public class UnfundedProjectTransaction : IUnfundedProjectTransaction
{
    public UnfundedProjectTransaction(Guid walletId, ProjectId projectId, ReleaseableTransactionDto dto, IFounderAppService founderAppService, UIServices uiServices)
    {
        Arrived = dto.Arrived;
        Released = dto.Released;
        Approved = dto.Approved;
        InvestmentEventId = dto.InvestmentEventId;

        Release = ReactiveCommand.CreateFromTask(() => UserFlow.PromptAndNotify(() => founderAppService.ReleaseInvestorTransactions(walletId, projectId, [InvestmentEventId]), uiServices,
                "Are you sure you want to release these funds?",
                "Confirm Release",
                "Success fully released",
                "Released",
                e => $"Cannot release the funds {e}"))
            .Enhance();
    }

    public DateTime Arrived { get; }
    public DateTime Approved { get; }
    public DateTime? Released { get; }
    public IEnhancedCommand<Maybe<Result>> Release { get; }
    public string InvestmentEventId { get; set; }
}

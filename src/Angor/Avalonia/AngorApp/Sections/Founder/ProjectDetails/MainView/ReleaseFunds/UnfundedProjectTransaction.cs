using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.UI.Services;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public class UnfundedProjectTransaction : IUnfundedProjectTransaction
{
    public UnfundedProjectTransaction(Guid walletId, ProjectId projectId, ReleaseableTransactionDto dto, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        Arrived = dto.Arrived;
        Released = dto.Released;
        Approved = dto.Approved;
        InvestorAddress = dto.InvestorAddress;

        Release = ReactiveCommand.CreateFromTask(() => UserFlow.PromptAndNotify(() => investmentAppService.ReleaseInvestorTransactions(walletId, projectId, [InvestorAddress]), uiServices,
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
    public string InvestorAddress { get; set; }
}
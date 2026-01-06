using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Shared.Services;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public class UnfundedProjectTransaction : IUnfundedProjectTransaction
{
    public UnfundedProjectTransaction(string walletId, ProjectId projectId, ReleaseableTransactionDto dto, IFounderAppService founderAppService, UIServices uiServices)
    {
        Arrived = dto.Arrived;
        Released = dto.Released;
        Approved = dto.Approved;
        InvestmentEventId = dto.InvestmentEventId;

        Release = ReactiveCommand.CreateFromTask(() => UserFlow.PromptAndNotify(
        async () =>
          {
              var result = await founderAppService.ReleaseFunds(new Angor.Sdk.Funding.Founder.Operations.ReleaseFunds.ReleaseFundsRequest(new WalletId(walletId), projectId, [InvestmentEventId]));
              return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
          },
        uiServices,
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

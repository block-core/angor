using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor;

public class InvestmentAppService(IMediator mediator) : IInvestmentAppService
{
    public Task<Result<InvestmentDraft>> CreateInvestmentDraft(WalletId sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate, byte? patternIndex = null, DateTime? investmentStartDate = null)
        => mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount, feerate, patternIndex, investmentStartDate));

    public Task<Result<Guid>> SubmitInvestment(WalletId sourceWalletId, ProjectId projectId, InvestmentDraft draft)
        => mediator.Send(new RequestInvestmentSignatures.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));

    public Task<Result> CancelInvestment(WalletId sourceWalletId, ProjectId projectId, string investmentId)
     => mediator.Send(new CancelInvestmentSignatures.CancelInvestmentSignaturesRequest(sourceWalletId, projectId, investmentId));

    public Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(WalletId walletId)
        => mediator.Send(new Investments.InvestmentsPortfolioRequest(walletId));

    public Task<Result> ConfirmInvestment(string investmentId, WalletId walletId, ProjectId projectId)
        => mediator.Send(new PublishInvestment.PublishInvestmentRequest(investmentId, walletId, projectId));

    public Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(WalletId walletId)
        => mediator.Send(new GetPenalties.GetPenaltiesRequest(walletId));

    public Task<Result<bool>> IsInvestmentAbovePenaltyThreshold(ProjectId projectId, Amount amount)
        => mediator.Send(new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, amount));

    #region Methods for Investor/Manage funds. Remove this region ASAP. It's only for clarity.

    public Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(WalletId walletId, ProjectId projectId)
      => mediator.Send(new GetInvestorProjectRecovery.GetInvestorProjectRecoveryRequest(walletId, projectId));

    public Task<Result<RecoveryTransactionDraft>> BuildRecoverInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate)
        => mediator.Send(new RecoverFunds.RecoverFundsRequest(walletId, projectId, feerate));

    public Task<Result<ReleaseTransactionDraft>> BuildReleaseInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate)
        => mediator.Send(new ReleaseFunds.ReleaseFundsRequest(walletId, projectId, feerate));

    public Task<Result<EndOfProjectTransactionDraft>> BuildClaimInvestorEndOfProjectFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate)
        => mediator.Send(new ClaimEndOfProject.ClaimEndOfProjectRequest(walletId, projectId, feerate));

    public Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, ProjectId projectId, TransactionDraft draft)
           => mediator.Send(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(walletId.Value, projectId, draft));

    #endregion
}

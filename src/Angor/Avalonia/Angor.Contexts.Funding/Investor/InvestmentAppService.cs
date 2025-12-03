using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IMediator mediator) : IInvestmentAppService
{
    public Task<Result<InvestmentDraft>> CreateInvestmentDraft(WalletId sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount, feerate));
    }

    public Task<Result<Guid>> SubmitInvestment(WalletId sourceWalletId, ProjectId projectId, InvestmentDraft draft)
    {
        return mediator.Send(new RequestInvestmentSignatures.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));
    }

    public Task<Result> CancelInvestment(WalletId sourceWalletId, ProjectId projectId, string investmentId)
    {
        return mediator.Send(new CancelInvestmentSignatures.CancelInvestmentSignaturesRequest(sourceWalletId, projectId, investmentId));
    }

    public Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(WalletId walletId)
    {
        return mediator.Send(new Investments.InvestmentsPortfolioRequest(walletId));
    }

    public Task<Result> ConfirmInvestment(string investmentId, WalletId walletId, ProjectId projectId)
    {
        return mediator.Send(new PublishInvestment.PublishInvestmentRequest(investmentId, walletId, projectId));
    }
    
    public Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(WalletId walletId)
    {
        return mediator.Send(new GetPenalties.GetPenaltiesRequest(walletId));
    }

    public Task<Result<bool>> IsInvestmentAbovePenaltyThreshold(ProjectId projectId, Amount amount)
    {
        return mediator.Send(new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, amount));
    }

    #region Methods for Investor/Manage funds. Remove this region ASAP. It's only for clarity.
    
    // Investor/Manage Funds: Retrieve recovery info for an investment. Also contains a list of InvestorStageItemDtos 
    public Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(WalletId walletId, ProjectId projectId)
    {
        return mediator.Send(new GetInvestorProjectRecovery.GetInvestorProjectRecoveryRequest(walletId, projectId));
    }

    // Investor/Manage Funds
    public Task<Result<RecoveryTransactionDraft>> BuildRecoverInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate)
    {
        return mediator.Send(new RecoverFunds.RecoverFundsRequest(walletId, projectId, feerate));
    }

    // Investor/Manage Funds
    public Task<Result<ReleaseTransactionDraft>> BuildReleaseInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate)
    {
        return mediator.Send(new ReleaseFunds.ReleaseFundsRequest(walletId, projectId, feerate));
    }

    // Investor/Manage Funds
    public Task<Result<EndOfProjectTransactionDraft>> BuildClaimInvestorEndOfProjectFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate)
    {
        return mediator.Send(new ClaimEndOfProject.ClaimEndOfProjectRequest(walletId, projectId, feerate));
    }

    public Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, ProjectId projectId, TransactionDraft draft)
    {
        return mediator.Send(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(walletId.Value, projectId, draft));
    }

    #endregion
}

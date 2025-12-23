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
    public Task<Result<CreateInvestment.CreateInvestmentTransactionResponse>> CreateInvestmentDraft(CreateInvestment.CreateInvestmentTransactionRequest request)
        => mediator.Send(request);

    public Task<Result<RequestInvestmentApproval.RequestInvestmentApprovalResponse>> RequestInvestmentApproval(RequestInvestmentApproval.RequestInvestmentApprovalRequest request)
        => mediator.Send(request);

    public Task<Result<CancelInvestmentSignatures.CancelInvestmentSignaturesResponse>> CancelInvestment(CancelInvestmentSignatures.CancelInvestmentSignaturesRequest request)
=> mediator.Send(request);

    public Task<Result<Investments.InvestmentsPortfolioResponse>> GetInvestorProjects(Investments.InvestmentsPortfolioRequest request)
        => mediator.Send(request);

  public Task<Result<PublishInvestment.PublishInvestmentResponse>> PublishInvestment(PublishInvestment.PublishInvestmentRequest request)
   => mediator.Send(request);

    public Task<Result<GetPenalties.GetPenaltiesResponse>> GetPenalties(GetPenalties.GetPenaltiesRequest request)
    => mediator.Send(request);

    public Task<Result<CheckPenaltyThreshold.CheckPenaltyThresholdResponse>> CheckPenaltyThreshold(CheckPenaltyThreshold.CheckPenaltyThresholdRequest request)
      => mediator.Send(request);

    #region Methods for Investor/Manage funds. Remove this region ASAP. It's only for clarity.

    public Task<Result<GetInvestorProjectRecovery.GetInvestorProjectRecoveryResponse>> GetInvestorProjectRecovery(GetInvestorProjectRecovery.GetInvestorProjectRecoveryRequest request)
   => mediator.Send(request);

    public Task<Result<RecoverFunds.RecoverFundsResponse>> BuildRecoverInvestorFunds(RecoverFunds.RecoverFundsRequest request)
        => mediator.Send(request);

    public Task<Result<ReleaseFunds.ReleaseFundsResponse>> BuildReleaseInvestorFunds(ReleaseFunds.ReleaseFundsRequest request)
   => mediator.Send(request);

    public Task<Result<ClaimEndOfProject.ClaimEndOfProjectResponse>> BuildClaimInvestorEndOfProjectFunds(ClaimEndOfProject.ClaimEndOfProjectRequest request)
=> mediator.Send(request);

    public Task<Result<PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionResponse>> PublishTransaction(PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest request)
 => mediator.Send(request);

    #endregion

    #region Methods for monitoring external funding

    public Task<Result<MonitorAddressForFunds.MonitorAddressForFundsResponse>> MonitorAddressForFunds(MonitorAddressForFunds.MonitorAddressForFundsRequest request)
        => mediator.Send(request);

    #endregion
}

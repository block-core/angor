using Angor.Sdk.Funding.Investor.Operations;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor;

public class InvestmentAppService(IMediator mediator) : IInvestmentAppService
{
    public Task<Result<BuildInvestmentDraft.BuildInvestmentDraftResponse>> BuildInvestmentDraft(BuildInvestmentDraft.BuildInvestmentDraftRequest request)
        => mediator.Send(request);

    public Task<Result<RequestInvestmentSignatures.RequestFounderSignaturesResponse>> SubmitInvestment(RequestInvestmentSignatures.RequestFounderSignaturesRequest request)
        => mediator.Send(request);

    public Task<Result<CancelInvestmentRequest.CancelInvestmentRequestResponse>> CancelInvestmentRequest(CancelInvestmentRequest.CancelInvestmentRequestRequest request)
        => mediator.Send(request);

    public Task<Result<GetInvestments.GetInvestmentsResponse>> GetInvestments(GetInvestments.GetInvestmentsRequest request)
        => mediator.Send(request);

    public Task<Result<PublishInvestment.PublishInvestmentResponse>> ConfirmInvestment(PublishInvestment.PublishInvestmentRequest request)
        => mediator.Send(request);

    public Task<Result<GetPenalties.GetPenaltiesResponse>> GetPenalties(GetPenalties.GetPenaltiesRequest request)
        => mediator.Send(request);

    public Task<Result<CheckPenaltyThreshold.CheckPenaltyThresholdResponse>> IsInvestmentAbovePenaltyThreshold(CheckPenaltyThreshold.CheckPenaltyThresholdRequest request)
        => mediator.Send(request);

    public Task<Result<GetInvestorNsec.GetInvestorNsecResponse>> GetInvestorNsec(GetInvestorNsec.GetInvestorNsecRequest request)
        => mediator.Send(request);

    #region Methods for Investor/Manage funds. Remove this region ASAP. It's only for clarity.

    public Task<Result<GetRecoveryStatus.GetRecoveryStatusResponse>> GetRecoveryStatus(GetRecoveryStatus.GetRecoveryStatusRequest request)
        => mediator.Send(request);

    public Task<Result<BuildRecoveryTransaction.BuildRecoveryTransactionResponse>> BuildRecoveryTransaction(BuildRecoveryTransaction.BuildRecoveryTransactionRequest request)
        => mediator.Send(request);

    public Task<Result<BuildReleaseTransaction.BuildReleaseTransactionResponse>> BuildReleaseTransaction(BuildReleaseTransaction.BuildReleaseTransactionRequest request)
        => mediator.Send(request);

    public Task<Result<BuildEndOfProjectClaim.BuildEndOfProjectClaimResponse>> BuildEndOfProjectClaim(BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest request)
        => mediator.Send(request);

    public Task<Result<PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionResponse>> SubmitTransactionFromDraft(PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest request)
        => mediator.Send(request);

    #endregion

    #region Methods for monitoring external funding

    public Task<Result<MonitorAddressForFunds.MonitorAddressForFundsResponse>> MonitorAddressForFunds(MonitorAddressForFunds.MonitorAddressForFundsRequest request, CancellationToken cancellationToken = default)
        => mediator.Send(request, cancellationToken);

    #endregion

    #region Methods for Lightning Network integration (Boltz submarine swaps)

    public Task<Result<CreateLightningSwapForInvestment.CreateLightningSwapResponse>> CreateLightningSwap(CreateLightningSwapForInvestment.CreateLightningSwapRequest request)
        => mediator.Send(request);

    public Task<Result<MonitorLightningSwap.MonitorLightningSwapResponse>> MonitorLightningSwap(MonitorLightningSwap.MonitorLightningSwapRequest request)
        => mediator.Send(request);

    public Task<Result<GetTotalInvested.GetTotalInvestedResponse>> GetTotalInvested(GetTotalInvested.GetTotalInvestedRequest request)
        => mediator.Send(request);

    #endregion
}

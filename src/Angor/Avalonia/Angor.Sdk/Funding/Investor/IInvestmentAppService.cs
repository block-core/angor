using Angor.Sdk.Funding.Investor.Operations;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<BuildInvestmentDraft.BuildInvestmentDraftResponse>> BuildInvestmentDraft(BuildInvestmentDraft.BuildInvestmentDraftRequest request);
    Task<Result<RequestInvestmentSignatures.RequestFounderSignaturesResponse>> SubmitInvestment(RequestInvestmentSignatures.RequestFounderSignaturesRequest request);
    Task<Result<CancelInvestmentRequest.CancelInvestmentRequestResponse>> CancelInvestmentRequest(CancelInvestmentRequest.CancelInvestmentRequestRequest request);
    Task<Result<GetInvestments.GetInvestmentsResponse>> GetInvestments(GetInvestments.GetInvestmentsRequest request);
    Task<Result<PublishInvestment.PublishInvestmentResponse>> ConfirmInvestment(PublishInvestment.PublishInvestmentRequest request);
    Task<Result<GetPenalties.GetPenaltiesResponse>> GetPenalties(GetPenalties.GetPenaltiesRequest request);

    // Methods for Investor - Manage funds
    Task<Result<GetRecoveryStatus.GetRecoveryStatusResponse>> GetRecoveryStatus(GetRecoveryStatus.GetRecoveryStatusRequest request);
    Task<Result<BuildRecoveryTransaction.BuildRecoveryTransactionResponse>> BuildRecoveryTransaction(BuildRecoveryTransaction.BuildRecoveryTransactionRequest request);
    Task<Result<BuildReleaseTransaction.BuildReleaseTransactionResponse>> BuildReleaseTransaction(BuildReleaseTransaction.BuildReleaseTransactionRequest request);
    Task<Result<BuildEndOfProjectClaim.BuildEndOfProjectClaimResponse>> BuildEndOfProjectClaim(BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest request);

    Task<Result<PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionResponse>> SubmitTransactionFromDraft(PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest request);

    Task<Result<CheckPenaltyThreshold.CheckPenaltyThresholdResponse>> IsInvestmentAbovePenaltyThreshold(CheckPenaltyThreshold.CheckPenaltyThresholdRequest request);

    // Methods for getting investor keys
    Task<Result<GetInvestorNsec.GetInvestorNsecResponse>> GetInvestorNsec(GetInvestorNsec.GetInvestorNsecRequest request);

    // Methods for monitoring external funding
    /// <summary>
    /// Monitors a specific address for incoming funds from an external wallet.
    /// Stores the request to DB, monitors the mempool, updates account info when funds are detected, and saves to DB.
    /// </summary>
    Task<Result<MonitorAddressForFunds.MonitorAddressForFundsResponse>> MonitorAddressForFunds(MonitorAddressForFunds.MonitorAddressForFundsRequest request, CancellationToken cancellationToken = default);

    // Methods for Lightning Network integration
    /// <summary>
    /// Creates a Lightning invoice for funding an investment
    /// </summary>
    Task<Result<CreateLightningInvoiceForInvestment.CreateLightningInvoiceResponse>> CreateLightningInvoiceForInvestment(CreateLightningInvoiceForInvestment.CreateLightningInvoiceRequest request);

    /// <summary>
    /// Monitors a Lightning invoice for payment and swaps to on-chain when paid
    /// </summary>
    Task<Result<MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceResponse>> MonitorLightningInvoiceAndSwap(MonitorLightningInvoiceAndSwap.MonitorLightningInvoiceRequest request);

    /// <summary>
    /// Orchestrates the complete flow: creates invoice, monitors payment, swaps to on-chain for investment
    /// </summary>
    Task<Result<FundInvestmentViaLightning.FundInvestmentViaLightningResponse>> FundInvestmentViaLightning(FundInvestmentViaLightning.FundInvestmentViaLightningRequest request);
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<CreateInvestment.CreateInvestmentTransactionResponse>> CreateInvestmentDraft(CreateInvestment.CreateInvestmentTransactionRequest request);
    Task<Result<RequestInvestmentApproval.RequestInvestmentApprovalResponse>> RequestInvestmentApproval(RequestInvestmentApproval.RequestInvestmentApprovalRequest request);
    Task<Result<CancelInvestmentSignatures.CancelInvestmentSignaturesResponse>> CancelInvestment(CancelInvestmentSignatures.CancelInvestmentSignaturesRequest request);
    Task<Result<Investments.InvestmentsPortfolioResponse>> GetInvestorProjects(Investments.InvestmentsPortfolioRequest request);
    Task<Result<PublishInvestment.PublishInvestmentResponse>> PublishInvestment(PublishInvestment.PublishInvestmentRequest request);
    Task<Result<GetPenalties.GetPenaltiesResponse>> GetPenalties(GetPenalties.GetPenaltiesRequest request);

    // Methods for Investor - Manage funds
    Task<Result<GetInvestorProjectRecovery.GetInvestorProjectRecoveryResponse>> GetInvestorProjectRecovery(GetInvestorProjectRecovery.GetInvestorProjectRecoveryRequest request);
    Task<Result<RecoverFunds.RecoverFundsResponse>> BuildRecoverInvestorFunds(RecoverFunds.RecoverFundsRequest request);
    Task<Result<ReleaseFunds.ReleaseFundsResponse>> BuildReleaseInvestorFunds(ReleaseFunds.ReleaseFundsRequest request);
    Task<Result<ClaimEndOfProject.ClaimEndOfProjectResponse>> BuildClaimInvestorEndOfProjectFunds(ClaimEndOfProject.ClaimEndOfProjectRequest request);

    Task<Result<PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionResponse>> PublishTransaction(PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest request);

    Task<Result<CheckPenaltyThreshold.CheckPenaltyThresholdResponse>> CheckPenaltyThreshold(CheckPenaltyThreshold.CheckPenaltyThresholdRequest request);

    // Methods for monitoring external funding
    /// <summary>
    /// Monitors a specific address for incoming funds from an external wallet.
    /// Stores the request to DB, monitors the mempool, updates account info when funds are detected, and saves to DB.
    /// </summary>
    Task<Result<MonitorAddressForFunds.MonitorAddressForFundsResponse>> MonitorAddressForFunds(MonitorAddressForFunds.MonitorAddressForFundsRequest request);
}

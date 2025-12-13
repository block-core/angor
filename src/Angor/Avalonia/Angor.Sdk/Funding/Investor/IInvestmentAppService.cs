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
    Task<Result<RequestInvestmentSignatures.RequestFounderSignaturesResponse>> SubmitInvestment(RequestInvestmentSignatures.RequestFounderSignaturesRequest request);
    Task<Result<CancelInvestmentSignatures.CancelInvestmentSignaturesResponse>> CancelInvestment(CancelInvestmentSignatures.CancelInvestmentSignaturesRequest request);
    Task<Result<Investments.InvestmentsPortfolioResponse>> GetInvestorProjects(Investments.InvestmentsPortfolioRequest request);
    Task<Result<PublishInvestment.PublishInvestmentResponse>> ConfirmInvestment(PublishInvestment.PublishInvestmentRequest request);
    Task<Result<GetPenalties.GetPenaltiesResponse>> GetPenalties(GetPenalties.GetPenaltiesRequest request);

    // Methods for Investor - Manage funds
    Task<Result<GetInvestorProjectRecovery.GetInvestorProjectRecoveryResponse>> GetInvestorProjectRecovery(GetInvestorProjectRecovery.GetInvestorProjectRecoveryRequest request);
    Task<Result<RecoverFunds.RecoverFundsResponse>> BuildRecoverInvestorFunds(RecoverFunds.RecoverFundsRequest request);
    Task<Result<ReleaseFunds.ReleaseFundsResponse>> BuildReleaseInvestorFunds(ReleaseFunds.ReleaseFundsRequest request);
    Task<Result<ClaimEndOfProject.ClaimEndOfProjectResponse>> BuildClaimInvestorEndOfProjectFunds(ClaimEndOfProject.ClaimEndOfProjectRequest request);

    Task<Result<PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionResponse>> SubmitTransactionFromDraft(PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest request);

    Task<Result<CheckPenaltyThreshold.CheckPenaltyThresholdResponse>> IsInvestmentAbovePenaltyThreshold(CheckPenaltyThreshold.CheckPenaltyThresholdRequest request);
}

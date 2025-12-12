using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<InvestmentDraft>> CreateInvestmentDraft(WalletId sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate, byte? patternIndex = null, DateTime? investmentStartDate = null);
    Task<Result<Guid>> SubmitInvestment(WalletId sourceWalletId, ProjectId projectId, InvestmentDraft draft);
    Task<Result> CancelInvestment(WalletId sourceWalletId, ProjectId projectId, string investmentId);
    Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(WalletId walletId);
    Task<Result> ConfirmInvestment(string investmentId, WalletId walletId, ProjectId projectId);
    Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(WalletId walletId);

    // Methods for Investor - Manage funds
    Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(WalletId walletId, ProjectId projectId);
    Task<Result<RecoveryTransactionDraft>> BuildRecoverInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<ReleaseTransactionDraft>> BuildReleaseInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<EndOfProjectTransactionDraft>> BuildClaimInvestorEndOfProjectFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate);

    Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, ProjectId projectId, TransactionDraft draft);

    Task<Result<bool>> IsInvestmentAbovePenaltyThreshold(ProjectId projectId, Amount amount);
}

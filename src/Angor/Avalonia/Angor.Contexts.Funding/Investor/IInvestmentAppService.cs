using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<InvestmentDraft>> CreateInvestmentDraft(WalletId sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate);
    Task<Result<Guid>> SubmitInvestment(WalletId sourceWalletId, ProjectId projectId, InvestmentDraft draft);
    Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(WalletId walletId);
    Task<Result> ConfirmInvestment(string investmentId, WalletId walletId, ProjectId projectId);
    Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(WalletId walletId);
    
    // Methods for Investor - Manage funds
    Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(WalletId walletId, ProjectId projectId);
    Task<Result<RecoveryTransactionDraft>> BuildRecoverInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<ReleaseTransactionDraft>> BuildReleaseInvestorFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<EndOfProjectTransactionDraft>> BuildClaimInvestorEndOfProjectFunds(WalletId walletId, ProjectId projectId, DomainFeerate feerate);
    
    Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, TransactionDraft draft);
    Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, ProjectId projectId, TransactionDraft draft);
    
    Task<Result<bool>> IsInvestmentAbovePenaltyThreshold(ProjectId projectId, Amount amount);
}

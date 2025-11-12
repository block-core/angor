using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<InvestmentDraft>> CreateInvestmentDraft(string sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate);
    Task<Result<Guid>> SubmitInvestment(string sourceWalletId, ProjectId projectId, InvestmentDraft draft);
    Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(string idValue);
    Task<Result> ConfirmInvestment(string investmentId, string walletId, ProjectId projectId);
    Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(string walletId);
    
    // Methods for Investor - Manage funds
    Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(string walletId, ProjectId projectId);
    Task<Result<RecoveryTransactionDraft>> BuildRecoverInvestorFunds(string walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<ReleaseTransactionDraft>> BuildReleaseInvestorFunds(string walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<EndOfProjectTransactionDraft>> BuildClaimInvestorEndOfProjectFunds(string walletId, ProjectId projectId, DomainFeerate feerate);
    
    Task<Result<string>> SubmitTransactionFromDraft(string walletId, TransactionDraft draft);
    Task<Result<string>> SubmitTransactionFromDraft(string walletId, ProjectId projectId, TransactionDraft draft);
    
    Task<Result<bool>> IsInvestmentAbovePenaltyThreshold(ProjectId projectId, Amount amount);
}

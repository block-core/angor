using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<InvestmentDraft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate);
    Task<Result<Guid>> SubmitInvestment(Guid sourceWalletId, ProjectId projectId, InvestmentDraft draft);
    Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(Guid idValue);
    Task<Result> ConfirmInvestment(string investmentId, Guid walletId, ProjectId projectId);
    Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(Guid walletId);
    
    // Methods for Investor - Manage funds
    Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(Guid walletId, ProjectId projectId);
    Task<Result<TransactionDraft>> BuildRecoverInvestorFunds(Guid walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<TransactionDraft>> BuildReleaseInvestorFunds(Guid walletId, ProjectId projectId, DomainFeerate feerate);
    Task<Result<TransactionDraft>> BuilodClaimInvestorEndOfProjectFunds(Guid walletId, ProjectId projectId, DomainFeerate feerate);
    
    Task<Result<string>> SubmitTransactionFromDraft(Guid walletId, TransactionDraft draft);
    
    /// <summary>
    /// Checks if an investment amount is above the penalty threshold for a given project.
    /// Returns true if the investment requires penalty path (founder approval needed).
    /// Returns false if the investment is below threshold (no penalty, direct investment).
    /// </summary>
    Task<Result<bool>> IsInvestmentAbovePenaltyThreshold(ProjectId projectId, Amount amount);
}

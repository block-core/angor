using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using Investment = Angor.Contexts.Funding.Founder.Operations.Investment;

namespace Angor.Contexts.Funding.Investor;

public interface IInvestmentAppService
{
    Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate);
    Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft);
    Task<Result<IEnumerable<Investment>>> GetInvestments(Guid walletId, ProjectId projectId);
    Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, Investment investment);
    Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(Guid idValue);
    Task<Result> ConfirmInvestment(string investmentId, Guid walletId, ProjectId projectId);
    Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(Guid walletId);
    Task<Result> Spend(Guid walletId, DomainFeerate fee, ProjectId projectId, IEnumerable<SpendTransactionDto> toSpend);
    Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(Guid walletId, ProjectId projectId);
    Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleaseableTransactions(Guid walletId, ProjectId projectId);
    Task<Result> ReleaseInvestorTransactions(Guid walletId, ProjectId projectId, IEnumerable<string> investorAddresses);
    
    // Methods for Investor/Manage funds
    Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(Guid walletId, ProjectId projectId);
    Task<Result> RecoverInvestorFunds(Guid walletId, ProjectId projectId, int stageIndex);
    Task<Result> ReleaseInvestorFunds(Guid walletId, ProjectId projectId, int stageIndex);
    Task<Result> ClaimInvestorEndOfProjectFunds(Guid walletId, ProjectId projectId, int stageIndex);
}

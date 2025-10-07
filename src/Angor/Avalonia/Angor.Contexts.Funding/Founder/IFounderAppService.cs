using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Founder;

public interface IFounderAppService
{
    Task<Result<IEnumerable<Investment>>> GetInvestments(Guid walletId, ProjectId projectId);
    Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, Investment investment);
    Task<Result> Spend(Guid walletId, DomainFeerate fee, ProjectId projectId, IEnumerable<SpendTransactionDto> toSpend);
    Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(Guid walletId, ProjectId projectId);
    Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleasableTransactions(Guid walletId, ProjectId projectId);
    Task<Result> ReleaseInvestorTransactions(Guid walletId, ProjectId projectId, IEnumerable<string> investorAddresses);
}
using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Founder;

public interface IFounderAppService
{
    Task<Result<IEnumerable<Investment>>> GetInvestments(string walletId, ProjectId projectId);
    Task<Result> ApproveInvestment(string walletId, ProjectId projectId, Investment investment);
    Task<Result<TransactionDraft>> Spend(string walletId, DomainFeerate fee, ProjectId projectId,
        IEnumerable<SpendTransactionDto> toSpend);
    Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(string walletId, ProjectId projectId);
    Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleasableTransactions(string walletId, ProjectId projectId);
    Task<Result> ReleaseInvestorTransactions(string walletId, ProjectId projectId, IEnumerable<string> investorAddresses);
    
    Task<Result<string>> SubmitTransactionFromDraft(string walletId, TransactionDraft draft);
}
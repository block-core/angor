using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Founder;

public interface IFounderAppService
{
    Task<Result<IEnumerable<Investment>>> GetInvestments(WalletId walletId, ProjectId projectId);
    Task<Result> ApproveInvestment(WalletId walletId, ProjectId projectId, Investment investment);
    Task<Result<TransactionDraft>> Spend(WalletId walletId, DomainFeerate fee, ProjectId projectId,
        IEnumerable<SpendTransactionDto> toSpend);
    Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(WalletId walletId, ProjectId projectId);
    Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleasableTransactions(WalletId walletId, ProjectId projectId);
    Task<Result> ReleaseInvestorTransactions(WalletId walletId, ProjectId projectId, IEnumerable<string> investorAddresses);
    
    Task<Result<ProjectSeedDto>> CreateNewProjectKeysAsync(WalletId walletId);
    Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, TransactionDraft draft);
    Task<Result<MoonshotProjectData>> GetMoonshotProject(string eventId);
}
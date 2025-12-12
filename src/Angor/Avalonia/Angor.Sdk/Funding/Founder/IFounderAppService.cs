using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Founder;

public interface IFounderAppService
{
    Task<Result<GetInvestments.GetInvestmentsResponse>> GetInvestments(WalletId walletId, ProjectId projectId);
    Task<Result<ApproveInvestment.ApproveInvestmentResponse>> ApproveInvestment(WalletId walletId, ProjectId projectId, Investment investment);
    Task<Result<SpendFounderStageTransaction.SpendFounderStageTransactionResponse>> Spend(WalletId walletId, DomainFeerate fee, ProjectId projectId,
        IEnumerable<SpendTransactionDto> toSpend);
    Task<Result<GetClaimableTransactions.GetClaimableTransactionsResponse>> GetClaimableTransactions(WalletId walletId, ProjectId projectId);
    Task<Result<GetReleaseableTransactions.GetReleaseableTransactionsResponse>> GetReleasableTransactions(WalletId walletId, ProjectId projectId);
    Task<Result<ReleaseInvestorTransaction.ReleaseInvestorTransactionResponse>> ReleaseInvestorTransactions(WalletId walletId, ProjectId projectId, IEnumerable<string> investorAddresses);
    
    Task<Result<CreateProjectNewKeys.CreateProjectNewKeysResponse>> CreateNewProjectKeysAsync(WalletId walletId);
    Task<Result<PublishFounderTransaction.PublishFounderTransactionResponse>> SubmitTransactionFromDraft(WalletId walletId, TransactionDraft draft);
    Task<Result<GetMoonshotProject.GetMoonshotProjectResponse>> GetMoonshotProject(string eventId);
}
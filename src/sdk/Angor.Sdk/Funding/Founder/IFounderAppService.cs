using Angor.Sdk.Funding.Founder.Operations;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Founder;

public interface IFounderAppService
{
    Task<Result<GetProjectInvestments.GetProjectInvestmentsResponse>> GetProjectInvestments(GetProjectInvestments.GetProjectInvestmentsRequest request);
    Task<Result<ApproveInvestment.ApproveInvestmentResponse>> ApproveInvestment(ApproveInvestment.ApproveInvestmentRequest request);
    Task<Result<SpendStageFunds.SpendStageFundsResponse>> SpendStageFunds(SpendStageFunds.SpendStageFundsRequest request);
    Task<Result<GetClaimableTransactions.GetClaimableTransactionsResponse>> GetClaimableTransactions(GetClaimableTransactions.GetClaimableTransactionsRequest request);
    Task<Result<GetReleasableTransactions.GetReleasableTransactionsResponse>> GetReleasableTransactions(GetReleasableTransactions.GetReleasableTransactionsRequest request);
    Task<Result<ReleaseFunds.ReleaseFundsResponse>> ReleaseFunds(ReleaseFunds.ReleaseFundsRequest request);
  
    Task<Result<CreateProjectKeys.CreateProjectKeysResponse>> CreateProjectKeys(CreateProjectKeys.CreateProjectKeysRequest request);
    Task<Result<PublishFounderTransaction.PublishFounderTransactionResponse>> SubmitTransactionFromDraft(PublishFounderTransaction.PublishFounderTransactionRequest request);
    Task<Result<GetMoonshotProject.GetMoonshotProjectResponse>> GetMoonshotProject(GetMoonshotProject.GetMoonshotProjectRequest request);
}
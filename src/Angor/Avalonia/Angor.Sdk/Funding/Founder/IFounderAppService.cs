using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Founder;

public interface IFounderAppService
{
    Task<Result<GetInvestments.GetInvestmentsResponse>> GetInvestments(GetInvestments.GetInvestmentsRequest request);
    Task<Result<ApproveInvestment.ApproveInvestmentResponse>> ApproveInvestment(ApproveInvestment.ApproveInvestmentRequest request);
    Task<Result<SpendFounderStageTransaction.SpendFounderStageTransactionResponse>> Spend(SpendFounderStageTransaction.SpendFounderStageTransactionRequest request);
    Task<Result<GetClaimableTransactions.GetClaimableTransactionsResponse>> GetClaimableTransactions(GetClaimableTransactions.GetClaimableTransactionsRequest request);
    Task<Result<GetReleasableTransactions.GetReleasableTransactionsResponse>> GetReleasableTransactions(GetReleasableTransactions.GetReleasableTransactionsRequest request);
    Task<Result<ReleaseInvestorTransaction.ReleaseInvestorTransactionResponse>> ReleaseInvestorTransactions(ReleaseInvestorTransaction.ReleaseInvestorTransactionRequest request);
  
    Task<Result<CreateProjectNewKeys.CreateProjectNewKeysResponse>> CreateNewProjectKeysAsync(CreateProjectNewKeys.CreateProjectNewKeysRequest request);
    Task<Result<PublishFounderTransaction.PublishFounderTransactionResponse>> SubmitTransactionFromDraft(PublishFounderTransaction.PublishFounderTransactionRequest request);
    Task<Result<GetMoonshotProject.GetMoonshotProjectResponse>> GetMoonshotProject(GetMoonshotProject.GetMoonshotProjectRequest request);
}
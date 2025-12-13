using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Founder;

public class FounderAppService(IMediator mediator) : IFounderAppService
{
    public Task<Result<GetInvestments.GetInvestmentsResponse>> GetInvestments(GetInvestments.GetInvestmentsRequest request) => 
        mediator.Send(request);

    public Task<Result<ApproveInvestment.ApproveInvestmentResponse>> ApproveInvestment(ApproveInvestment.ApproveInvestmentRequest request) => 
        mediator.Send(request);
    
    public Task<Result<SpendFounderStageTransaction.SpendFounderStageTransactionResponse>> Spend(SpendFounderStageTransaction.SpendFounderStageTransactionRequest request) => 
        mediator.Send(request);

    public Task<Result<GetClaimableTransactions.GetClaimableTransactionsResponse>> GetClaimableTransactions(GetClaimableTransactions.GetClaimableTransactionsRequest request) => 
        mediator.Send(request);
    
    public Task<Result<GetReleaseableTransactions.GetReleaseableTransactionsResponse>> GetReleasableTransactions(GetReleaseableTransactions.GetReleaseableTransactionsRequest request) => 
   mediator.Send(request);

    public Task<Result<ReleaseInvestorTransaction.ReleaseInvestorTransactionResponse>> ReleaseInvestorTransactions(ReleaseInvestorTransaction.ReleaseInvestorTransactionRequest request) => 
        mediator.Send(request);
    
    public Task<Result<CreateProjectNewKeys.CreateProjectNewKeysResponse>> CreateNewProjectKeysAsync(CreateProjectNewKeys.CreateProjectNewKeysRequest request) =>
        mediator.Send(request);
    
    public Task<Result<PublishFounderTransaction.PublishFounderTransactionResponse>> SubmitTransactionFromDraft(PublishFounderTransaction.PublishFounderTransactionRequest request) => 
mediator.Send(request);

    public Task<Result<GetMoonshotProject.GetMoonshotProjectResponse>> GetMoonshotProject(GetMoonshotProject.GetMoonshotProjectRequest request) =>
    mediator.Send(request);
}
using Angor.Sdk.Funding.Founder.Operations;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Founder;

public class FounderAppService(IMediator mediator) : IFounderAppService
{
    public Task<Result<GetProjectInvestments.GetProjectInvestmentsResponse>> GetProjectInvestments(GetProjectInvestments.GetProjectInvestmentsRequest request) => mediator.Send(request);

    public Task<Result<ApproveInvestment.ApproveInvestmentResponse>> ApproveInvestment(ApproveInvestment.ApproveInvestmentRequest request) => mediator.Send(request);
    
    public Task<Result<SpendStageFunds.SpendStageFundsResponse>> SpendStageFunds(SpendStageFunds.SpendStageFundsRequest request) => mediator.Send(request);

    public Task<Result<GetClaimableTransactions.GetClaimableTransactionsResponse>> GetClaimableTransactions(GetClaimableTransactions.GetClaimableTransactionsRequest request) => mediator.Send(request);
    
    public Task<Result<GetReleasableTransactions.GetReleasableTransactionsResponse>> GetReleasableTransactions(GetReleasableTransactions.GetReleasableTransactionsRequest request) => mediator.Send(request);

    public Task<Result<ReleaseFunds.ReleaseFundsResponse>> ReleaseFunds(ReleaseFunds.ReleaseFundsRequest request) => mediator.Send(request);
    
    public Task<Result<CreateProjectKeys.CreateProjectKeysResponse>> CreateProjectKeys(CreateProjectKeys.CreateProjectKeysRequest request) => mediator.Send(request);
    
    public Task<Result<PublishFounderTransaction.PublishFounderTransactionResponse>> SubmitTransactionFromDraft(PublishFounderTransaction.PublishFounderTransactionRequest request) => mediator.Send(request);

    public Task<Result<GetMoonshotProject.GetMoonshotProjectResponse>> GetMoonshotProject(GetMoonshotProject.GetMoonshotProjectRequest request) => mediator.Send(request);
}
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
    public Task<Result<IEnumerable<Investment>>> GetInvestments(WalletId walletId, ProjectId projectId) => 
        mediator.Send(new GetInvestments.GetInvestmentsRequest(walletId, projectId));

    public Task<Result> ApproveInvestment(WalletId walletId, ProjectId projectId, Investment investment) => 
        mediator.Send(new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment));
    
    public Task<Result<TransactionDraft>> Spend(WalletId walletId, DomainFeerate fee, ProjectId projectId, IEnumerable<SpendTransactionDto> toSpend) => 
        mediator.Send(new SpendFounderStageTransaction.SpendFounderStageTransactionRequest(walletId, projectId, new FeeEstimation { FeeRate = fee.SatsPerVByte }, toSpend));

    public Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(WalletId walletId, ProjectId projectId) => 
        mediator.Send(new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId));
    
    public Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleasableTransactions(WalletId walletId, ProjectId projectId) => 
        mediator.Send(new GetReleaseableTransactions.GetReleaseableTransactionsRequest(walletId, projectId));

    public Task<Result> ReleaseInvestorTransactions(WalletId walletId, ProjectId projectId, IEnumerable<string> investorAddresses) => 
        mediator.Send(new ReleaseInvestorTransaction.ReleaseInvestorTransactionRequest(walletId, projectId, investorAddresses));
    
    public Task<Result<ProjectSeedDto>> CreateNewProjectKeysAsync(WalletId walletId) =>
        mediator.Send(new CreateProjectNewKeys.CreateProjectNewKeysRequest(walletId));
    
    public Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, TransactionDraft draft) => 
        mediator.Send(new PublishFounderTransaction.PublishFounderTransactionRequest(draft));

    public Task<Result<MoonshotProjectData>> GetMoonshotProject(string eventId) =>
        mediator.Send(new GetMoonshotProject.GetMoonshotProjectRequest(eventId));
}
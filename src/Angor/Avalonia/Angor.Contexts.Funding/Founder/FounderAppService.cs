using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder;

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
    
    public Task<Result<string>> SubmitTransactionFromDraft(WalletId walletId, TransactionDraft draft) => 
        mediator.Send(new PublishTransaction.PublishTransactionRequest(null, null, draft));
}
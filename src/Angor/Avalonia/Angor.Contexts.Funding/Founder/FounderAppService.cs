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
    public Task<Result<IEnumerable<Investment>>> GetInvestments(string walletId, ProjectId projectId)
    {
        return mediator.Send(new GetInvestments.GetInvestmentsRequest(walletId, projectId));
    }

    public Task<Result> ApproveInvestment(string walletId, ProjectId projectId, Investment investment)
    {
        return mediator.Send(new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment));
    }
    
    public Task<Result<TransactionDraft>> Spend(string walletId, DomainFeerate fee, ProjectId projectId, IEnumerable<SpendTransactionDto> toSpend)
    {
        return mediator.Send(new SpendInvestorTransaction.SpendInvestorTransactionRequest(walletId, projectId,
            new FeeEstimation { FeeRate = fee.SatsPerVByte }, toSpend));
    }

    public Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(string walletId, ProjectId projectId)
    {
        return mediator.Send(new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId));
    }
    
    // Gets a list of all the transactions than can be released for a given project
    public Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleasableTransactions(string walletId, ProjectId projectId)
    {
        return mediator.Send(new GetReleaseableTransactions.GetReleaseableTransactionsRequest(walletId, projectId));
    }

    // Note: you can release multiple investors at once, hence the list
    public Task<Result> ReleaseInvestorTransactions(string walletId, ProjectId projectId, IEnumerable<string> investorAddresses)
    {
        return mediator.Send(new ReleaseInvestorTransaction.ReleaseInvestorTransactionRequest(walletId, projectId, investorAddresses));
    }
    
    public Task<Result<string>> SubmitTransactionFromDraft(string walletId, TransactionDraft draft)
    {
        return mediator.Send(new PublishTransaction.PublishTransactionRequest(null, null, draft));
    }
}
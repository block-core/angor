using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;
using Investment = Angor.Contexts.Funding.Founder.Operations.Investment;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IMediator mediator) : IInvestmentAppService
{
    public Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount, feerate));
    }

    public Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft)
    {
        return mediator.Send(new RequestInvestmentSignatures.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));
    }

    public Task<Result<IEnumerable<Investment>>> GetInvestments(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetInvestments.GetInvestmentsRequest(walletId, projectId));
    }

    public Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, Investment investment)
    {
        return mediator.Send(new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment));
    }

    public Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(Guid walletId)
    {
        return mediator.Send(new Investments.InvestmentsPortfolioRequest(walletId));
    }

    public Task<Result> ConfirmInvestment(string investmentId, Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new PublishInvestment.PublishInvestmentRequest(investmentId, walletId, projectId));
    }
    
    public Task<Result<IEnumerable<PenaltiesDto>>> GetPenalties(Guid walletId)
    {
        return mediator.Send(new GetPenalties.GetPenaltiesRequest(walletId));
    }

    public Task<Result> Spend(Guid walletId, DomainFeerate fee, ProjectId projectId, IEnumerable<SpendTransactionDto> toSpend)
    {
        return mediator.Send(
            new SpendInvestorTransaction.SpendInvestorTransactionRequest(walletId, projectId,
                new FeeEstimation { FeeRate = fee.SatsPerVByte }, toSpend));
    }

    public Task<Result<IEnumerable<ClaimableTransactionDto>>> GetClaimableTransactions(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId));
    }
    
    // Gets a list of all the transactions than can be released for a given project
    public Task<Result<IEnumerable<ReleaseableTransactionDto>>> GetReleaseableTransactions(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetReleaseableTransactions.GetReleaseableTransactionsRequest(walletId, projectId));
    }

    // Note: you can release multiple investors at once, hence the list
    public Task<Result> ReleaseInvestorTransactions(Guid walletId, ProjectId projectId, IEnumerable<string> investorAddresses)
    {
        return mediator.Send(new ReleaseInvestorTransaction.ReleaseInvestorTransactionRequest(walletId, projectId, investorAddresses));
    }
}
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IMediator mediator) : IInvestmentAppService
{
    public Task<Result<InvestmentDraft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount, feerate));
    }

    public Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, InvestmentDraft draft)
    {
        return mediator.Send(new RequestInvestmentSignatures.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));
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

    #region Methods for Investor/Manage funds. Remove this region ASAP. It's only for clarity.
    
    // Investor/Manage Funds: Retrieve recovery info for an investment. Also contains a list of InvestorStageItemDtos 
    public Task<Result<InvestorProjectRecoveryDto>> GetInvestorProjectRecovery(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetInvestorProjectRecovery.GetInvestorProjectRecoveryRequest(walletId, projectId));
    }

    // Investor/Manage Funds
    public Task<Result<TransactionDraft>> RecoverInvestorFunds(Guid walletId, ProjectId projectId, int stageIndex)
    {
        return mediator.Send(new RecoverFunds.RecoverFundsRequest(walletId, projectId, stageIndex));
    }

    // Investor/Manage Funds
    public Task<Result<TransactionDraft>> ReleaseInvestorFunds(Guid walletId, ProjectId projectId, int stageIndex)
    {
        return mediator.Send(new ReleaseFunds.ReleaseFundsRequest(walletId, projectId, stageIndex));
    }

    // Investor/Manage Funds
    public Task<Result<TransactionDraft>> ClaimInvestorEndOfProjectFunds(Guid walletId, ProjectId projectId, int stageIndex)
    {
        return mediator.Send(new ClaimEndOfProject.ClaimEndOfProjectRequest(walletId, projectId, stageIndex));
    }
    
    #endregion
}

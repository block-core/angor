using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IInvestmentRepository investmentRepository, IMediator mediator
    ) : IInvestmentAppService
{
    public Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId)
    {
        return investmentRepository.GetByProject(projectId);
    }

    public Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount));
    }

    public Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft)
    {
        return mediator.Send(new RequestInvestment.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));
    }
    
    public Task<Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetPendingInvestments.GetPendingInvestmentsRequest(walletId, projectId));
    }
}
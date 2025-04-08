using Angor.Contexts.Funding.Investor.Dtos;
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

    public Task<Result<CreateInvestment.Draft>> CreateDraft(Guid sourceWalletId, ProjectId projectId, Amount amount)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount));
    }

    public Task<Result<Guid>> RequestInvestment(ProjectId projectId, CreateInvestment.Draft draft)
    {
        return mediator.Send(new RequestInvestment.RequestFounderSignaturesRequest(projectId, draft));
    }
}
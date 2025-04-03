using Angor.Contexts.Funding.Investor.CreateInvestment;
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

    public Task<Result<InvestmentTransaction>> CreateInvestmentTransaction(Guid walletId, ProjectId projectId, Amount amount)
    {
        return mediator.Send(new CreateInvestmentTransactionRequest(walletId, projectId, amount));
    }

    public Task<Result<Guid>> RequestFounderSignatures(ProjectId projectId, InvestmentTransaction investmentTransaction)
    {
        return mediator.Send(new RequestFounderSignaturesRequest(projectId, investmentTransaction));
    }
}
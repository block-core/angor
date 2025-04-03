using Angor.Contexts.Funding.Investment;
using Angor.Contexts.Funding.Investment.Commands.CreateInvestment;
using Angor.Contexts.Funding.Investment.Dtos;
using Angor.Contexts.Funding.Investor.Requests.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IInvestmentRepository investmentRepository, 
    CreateInvestmentTransactionRequest createInvestmentTransactionRequest) : IInvestmentAppService
{
    public Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId)
    {
        return investmentRepository.GetByProject(projectId);
    }

    public Task<Result<PendingInvestment>> CreateInvestmentTransaction(Guid walletId, ProjectId projectId, Amount amount)
    {
        return createInvestmentTransactionRequest.Execute(walletId, projectId, amount);
    }
}
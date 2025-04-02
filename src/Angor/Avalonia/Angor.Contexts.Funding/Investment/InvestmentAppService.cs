using Angor.Contexts.Funding.Investment.Commands.CreateInvestment;
using Angor.Contexts.Funding.Investment.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investment;

public class InvestmentAppService(IInvestmentRepository investmentRepository, 
    CreateInvestmentTransactionCommand.Factory factory) : IInvestmentAppService
{
    public Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId)
    {
        return investmentRepository.GetByProject(projectId);
    }

    public Task<Result<PendingInvestment>> CreateInvestmentTransaction(Guid walletId, ProjectId projectId, Amount amount)
    {
        var command = factory.Create(walletId, projectId, amount);
        return command.Execute();
    }
}
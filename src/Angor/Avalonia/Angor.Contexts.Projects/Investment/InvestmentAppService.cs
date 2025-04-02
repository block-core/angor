using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.CreateInvestment;
using Angor.Contexts.Projects.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Investment;

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
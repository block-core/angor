using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Projects.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Investment;

public class InvestmentAppService(IInvestmentRepository investmentRepository) : IInvestmentAppService
{
    public Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId)
    {
        return investmentRepository.GetByProject(projectId);
    }
}
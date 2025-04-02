using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Investment;

public interface IInvestmentAppService
{
    Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId);
}
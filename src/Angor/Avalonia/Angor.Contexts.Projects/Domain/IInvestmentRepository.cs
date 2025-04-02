using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Interfaces;

public interface IInvestmentRepository
{
    Task<Result> Save(Investment investment);
    Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId);
    Task<Result<IList<InvestmentDto>>> GetByProject(ProjectId projectId);
}
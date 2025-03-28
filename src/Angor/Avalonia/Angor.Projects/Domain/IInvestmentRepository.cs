using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IInvestmentRepository
{
    Task<Result> Save(Investment investment);
    Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId);
    Task<Result<IList<InvestmentDto>>> GetByProject(ProjectId projectId);
}
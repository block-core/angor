using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Projects;

public class InvestmentRepository : IInvestmentRepository
{
    public async Task<Result> Save(Investment investment)
    {
        return Result.Success();
    }

    public async Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId)
    {
        return Result.Success<IEnumerable<Investment>>(new List<Investment>());
    }
}
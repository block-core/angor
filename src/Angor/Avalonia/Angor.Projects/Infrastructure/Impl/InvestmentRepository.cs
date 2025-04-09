using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestmentRepository : IInvestmentRepository
{
    public Task<Result> Save(Investment investment)
    {
        throw new NotImplementedException();
    }

    public Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId)
    {
        throw new NotImplementedException();
    }
}
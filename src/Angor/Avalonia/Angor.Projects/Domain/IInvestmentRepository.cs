using CSharpFunctionalExtensions;

namespace Angor.Projects.Domain;

public interface IInvestmentRepository
{
    Task<Result> Save(Investment investment);
    Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId);
}
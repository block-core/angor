using Angor.Contexts.Funding.Investor.Dtos;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Domain;

public interface IInvestmentRepository
{
    Task<Result<IEnumerable<InvestmentDto>>> GetByProjectAsync(ProjectId projectId);
    Task<Result> AddAsync(Guid walletId, Investment investment);
    Task<Result<Investment?>> GetAsync(Guid walletId, ProjectId projectId);
    Task<Result<IEnumerable<Investment>>> GetAllAsync(Guid walletId);
    Task<Result> UpdateAsync(Guid walletId, Investment investment);
}
using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Interfaces;

public interface IInvestmentRepository
{
    Task<Result> Add(Guid walletId, Investment investment);
    Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId);
}
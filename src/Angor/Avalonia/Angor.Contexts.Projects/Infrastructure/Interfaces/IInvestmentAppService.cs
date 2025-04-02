using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Interfaces;

public interface IInvestmentAppService
{
    Task<Result> Invest(Guid walletId, ProjectId projectId, Amount amount);
    Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId);
}
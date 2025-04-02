using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<IList<ProjectDto>> Latest();
    Task<Maybe<ProjectDto>> FindById(ProjectId projectId);
    Task<Result> Invest(Guid walletId, ProjectId projectId, Amount amount);
    Task<Result<IList<InvestmentDto>>> GetInvestments(ProjectId projectId);
}
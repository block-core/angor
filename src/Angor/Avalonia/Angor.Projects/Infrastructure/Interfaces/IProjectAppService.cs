using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<IList<ProjectDto>> Latest();
    Task<Maybe<ProjectDto>> FindById(ProjectId projectId);
    Task<Result> Invest(Guid walletId, ProjectId projectId, Amount amount);
}
using Angor.Projects.Application.Dtos;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IProjectAppService
{
    Task<IList<ProjectDto>> Latest();
    Task<Maybe<ProjectDto>> FindById(string projectId);
}
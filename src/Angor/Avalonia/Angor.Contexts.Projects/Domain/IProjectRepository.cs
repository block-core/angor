using Angor.Contexts.Projects.Infrastructure.Impl.Commands;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Domain;

public interface IProjectRepository
{
    Task<Result<Project>> Get(ProjectId id);
    Task<IList<Project>> Latest();
  
}
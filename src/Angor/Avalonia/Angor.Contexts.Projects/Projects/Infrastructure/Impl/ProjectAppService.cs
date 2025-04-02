using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Projects.Projects.Domain;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Amount = Angor.Contexts.Projects.Domain.Amount;
using Domain_Amount = Angor.Contexts.Projects.Domain.Amount;

namespace Angor.Contexts.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository, InvestCommandFactory investmentCommandFactory)
    : IProjectAppService
{
    public Task<Result> Invest(Guid walletId, ProjectId projectId, Domain_Amount amount)
    {
        var command = investmentCommandFactory.Create(walletId, projectId, amount);
        return command.Execute();
    }

    public Task<Result<IEnumerable<InvestmentDto>>> GetInvestments(ProjectId projectId)
    {
        return investmentRepository.GetByProject(projectId);
    }

    public async Task<IList<ProjectDto>> Latest()
    {
        var projects = await projectRepository.Latest();
        var projectDtos = projects.Select(project => project.ToDto());
        return projectDtos.ToList();
    }

    public Task<Maybe<ProjectDto>> FindById(ProjectId projectId)
    {
        return projectRepository.Get(projectId).Map(project1 => project1.ToDto()).AsMaybe();
    }
}
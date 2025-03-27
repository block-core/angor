using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestorTransactionActions investorTransactionActions,
    IInvestorKeyProvider investorKeyProvider)
    : IProjectAppService
{
    public async Task<Result> Invest(Guid walletId, ProjectId projectId, Amount amount, ModelFeeRate feeRate)
    {
        var projectResult = await projectRepository.Get(projectId);
        if (projectResult.IsFailure)
        {
            return Result.Failure(projectResult.Error);
        }

        var project = projectResult.Value;

        var projectInfo = new ProjectInfo()
        {
        };

        // 2. Get investor data
        var investorKeyResult = await investorKeyProvider.InvestorKey(walletId, project.FounderKey);

        if (investorKeyResult.IsFailure)
        {
            return Result.Failure(investorKeyResult.Error);
        }

        // 3. Create invest transaction
        var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKeyResult.Value, amount.Sats));
        
        if (transactionResult.IsFailure)
        {
            return Result.Failure(transactionResult.Error);
        }

        // 4. Create and save investment
        var investment = Investment.Create(project.Id, investorKeyResult.Value, amount.Sats);
        await investmentRepository.Save(investment);

        return Result.Success();
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
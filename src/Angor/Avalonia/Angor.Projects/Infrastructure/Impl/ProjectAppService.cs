using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.ProtocolNew;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace Angor.Projects.Infrastructure.Impl;

public class ProjectAppService(
    IProjectRepository projectRepository,
    IInvestmentRepository investmentRepository,
    IInvestmentService bitcoinService,
    IRelayService relayService,
    IIndexerService indexerService,
    IInvestorTransactionActions investorTransactionActions,
    ISensibleDataProvider sensibleDataProvider,
    IDerivationOperations derivationOperations)
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

        var secrets = await sensibleDataProvider.GetSecrets(walletId);

        if (secrets.IsFailure)
        {
            return Result.Failure(secrets.Error);
        }

        var valueSeed = secrets.Value.seed;
        var valuePassphrase = secrets.Value.passphrase;
        var walletWords = new WalletWords { Words = valueSeed, Passphrase = valuePassphrase.GetValueOrDefault("") };
        var investorKey = derivationOperations.DeriveInvestorKey(walletWords, projectResult.Value.FounderKey);

        investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKey, amount.Sats);
        // 2. Get investor data
        var investorId = "...";
        var investorPubKey = "...";
        var projectAddress = "...";

        // 3. Create invest transaction
        var transactionResult = await bitcoinService.CreateInvestmentTransaction(
            projectAddress,
            investorPubKey,
            amount.Sats,
            feeRate
        );

        if (transactionResult.IsFailure)
        {
            return Result.Failure(transactionResult.Error);
        }

        // 4. Create and save investment
        var investment = Investment.Create(project.Id, investorId, amount.Sats);
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

public static class ProjectExtensions
{
    public static ProjectDto ToDto(this Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            ShortDescription = project.ShortDescription,
            StartingDate = project.StartingDate,
            TargetAmount = (long)(project.TargetAmount *  1_0000_0000),
            Stages = project.Stages.Select(stage => new StageDto
            {
                Amount = stage.Amount,
                Index = stage.Index,
                Weight = stage.Weight,
                ReleaseDate = stage.ReleaseDate,
            }).ToList()
        };
    }
}
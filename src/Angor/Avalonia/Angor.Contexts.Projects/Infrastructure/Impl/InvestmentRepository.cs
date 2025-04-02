using Angor.Client.Models;
using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Projects.Application.Dtos;
using Angor.Contexts.Projects.Domain;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.CreateInvestment;
using Angor.Contexts.Projects.Infrastructure.Impl.Commands.Investment.CreateInvestment;
using Angor.Contexts.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Projects.Infrastructure.Impl;

public class InvestmentRepository(
    IIndexerService indexerService,
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    IInvestorKeyProvider investorKeyProvider,
    IRelayService relayService) : IInvestmentRepository
{
    public async Task<Result> Add(Guid walletId, Investment newInvestment)
    {
        // Obtener todas las inversiones confirmadas existentes
        var confirmedInvestments = await GetAllConfirmedInvestments();

        // Agregar la nueva inversión si no existe ya
        if (!confirmedInvestments.Any(x => x.ProjectId.Value == newInvestment.ProjectId.Value
                                           && x.InvestorPubKey == newInvestment.InvestorPubKey))
        {
            confirmedInvestments.Add(newInvestment);
        }

        var investmentStates = confirmedInvestments.Select(inv => new InvestmentState
        {
            ProjectIdentifier = inv.ProjectId.Value,
            InvestorPubKey = inv.InvestorPubKey,
            InvestmentTransactionHash = inv.TransactionId
        }).ToList();
        
        var investments = new Investments { ProjectIdentifiers = investmentStates };

        // Encriptar y enviar todo junto a Nostr
        var sensiveDataResult = await investorKeyProvider.GetSensitiveData(walletId);
        if (sensiveDataResult.IsFailure)
        {
            return Result.Failure(sensiveDataResult.Error);
        }

        var words = sensiveDataResult.Value.ToWalletWords();
        var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
        var storageKey = derivationOperations.DeriveNostrStorageKey(words);
        var storageKeyHex = Encoders.Hex.EncodeData(storageKey.ToBytes());
        var password = derivationOperations.DeriveNostrStoragePassword(words);

        var encrypted = await encryptionService.EncryptData(serializer.Serialize(investments), password);

        var tcs = new TaskCompletionSource<bool>();
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result =>
        {
            tcs.SetResult(result.Accepted);
        });

        var success = await tcs.Task;
        return success ? Result.Success() : Result.Failure("Error adding investment");
    }

    public Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId)
    {
        return GetProjectInvestments(projectId).Map(enumerable => enumerable.Select(inv => new InvestmentDto
        {
            ProjectId = projectId,
            InvestorKey = inv.InvestorPubKey,
            Amount = inv.AmountInSatoshis,
            TransactionId = inv.TransactionId
        }));
    }

    public Task<Result<PendingInvestment>> GetPendingInvestment(Guid walletId, ProjectId projectId)
    {
        throw new NotImplementedException();
    }

    public Task<Result<SignedInvestment>> GetSignedInvestment(Guid walletId, ProjectId projectId)
    {
        throw new NotImplementedException();
    }

    private async Task<List<Investment>> GetAllConfirmedInvestments()
    {
        var allProjects = await indexerService.GetProjectsAsync(0, 20);
        var investments = new List<Investment>();

        foreach (var project in allProjects)
        {
            var projectId = new ProjectId(project.ProjectIdentifier);
            var projectInvestments = await GetProjectInvestments(projectId);

            if (projectInvestments.IsSuccess)
            {
                investments.AddRange(projectInvestments.Value);
            }
        }

        return investments;
    }

    private Task<Result<IEnumerable<Investment>>> GetProjectInvestments(ProjectId projectId)
    {
        return Result.Try(() => indexerService.GetInvestmentsAsync(projectId.Value))
            .Map(investments => investments.Select(inv => Investment.Create(projectId, inv.InvestorPublicKey, inv.TotalAmount, inv.TransactionId)));
    }
}
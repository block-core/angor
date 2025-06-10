using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class InvestmentRepository(
    IIndexerService indexerService,
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    ISeedwordsProvider seedwordsProvider,
    IRelayService relayService) : IInvestmentRepository
{
    public async Task<Result> Add(Guid walletId, Domain.Investment newInvestment)
    {
        // Get all confirmed investments
        var confirmedInvestments = await GetAllConfirmedInvestments();

        // Add the new investment if it doesn't already exist
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

        // Encrypt and send the investments
        var sensiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
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
        relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted, result => { tcs.SetResult(result.Accepted); });

        var success = await tcs.Task;
        return success ? Result.Success() : Result.Failure("Error adding investment");
    }

    public Task<Result<IEnumerable<InvestmentDto>>> GetByProject(ProjectId projectId)
    {
        return GetProjectInvestments(projectId).Map(enumerable => enumerable.Select(inv => new InvestmentDto
        {
            ProjectId = projectId,
            InvestorKey = inv.InvestorPubKey,
            AmountInSats = inv.Amount.Sats,
            TransactionId = inv.TransactionId
        }));
    }

    private async Task<List<Investment>> GetAllConfirmedInvestments()
    {
        var allProjects = await indexerService.GetProjectsAsync(0, 20);
        var investments = new List<Domain.Investment>();

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

    private Task<Result<IEnumerable<Domain.Investment>>> GetProjectInvestments(ProjectId projectId)
    {
        return Result.Try(() => indexerService.GetInvestmentsAsync(projectId.Value))
            .Map(investments => investments.Select(inv => Domain.Investment.Create(projectId, inv.InvestorPublicKey, new Amount(inv.TotalAmount), inv.TransactionId)));
    }
}
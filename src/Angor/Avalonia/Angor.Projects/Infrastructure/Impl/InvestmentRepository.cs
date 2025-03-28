using Angor.Client.Models;
using Angor.Client.Services;
using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestmentRepository(
    IIndexerService indexerService,
    IEncryptionService encryptionService,
    IDerivationOperations derivationOperations,
    ISerializer serializer,
    IRelayService relayService) : IInvestmentRepository
{
    public async Task<Result> Save(Investment investment)
    {
        try
        {
            var words = GetTestSeedwords();

            var storageAccountKey = derivationOperations.DeriveNostrStoragePubKeyHex(words);
            var storageKey = derivationOperations.DeriveNostrStorageKey(words);
            var storageKeyHex = Encoders.Hex.EncodeData(storageKey.ToBytes());
            var password = derivationOperations.DeriveNostrStoragePassword(words);

            var allProjects = await indexerService.GetProjectsAsync(0, 20);
            var investmentStates = new List<InvestmentState>();

            foreach (var project in allProjects)
            {
                var projectId = new ProjectId(project.ProjectIdentifier);
                var projectInvestments = await GetByProject(projectId);

                if (projectInvestments.IsSuccess)
                {
                    foreach (var inv in projectInvestments.Value)
                    {
                        investmentStates.Add(new InvestmentState
                        {
                            ProjectIdentifier = inv.ProjectId.Value,
                            InvestorPubKey = inv.InvestorKey,
                            InvestmentTransactionHash = inv.TransactionId,
                        });
                    }
                }
            }

            investmentStates.Add(new InvestmentState
            {
                ProjectIdentifier = investment.ProjectId.Value,
                InvestorPubKey = investment.InvestorPubKey,
                InvestmentTransactionHash = investment.TransactionId
            });

            var investments = new Investments { ProjectIdentifiers = investmentStates };
            var encrypted = await encryptionService.EncryptData(serializer.Serialize(investments), password);

            var tcs = new TaskCompletionSource<bool>();
            relayService.SendDirectMessagesForPubKeyAsync(storageKeyHex, storageAccountKey, encrypted,
                x => { tcs.SetResult(x.Accepted); });

            var result = await tcs.Task;
            return result
                ? Result.Success()
                : Result.Failure($"Error saving the investment");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error saving the investment: {ex.Message}");
        }
    }

    private WalletWords GetTestSeedwords()
    {
        var words = "print foil moment average quarter keep amateur shell tray roof acoustic where";
        var passphrase = "";
        return new WalletWords()
        {
            Words = words,
            Passphrase = passphrase
        };
    }

    public async Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId)
    {
        // TODO: Get investments for the project
        return Result.Success<IEnumerable<Investment>>(new List<Investment>());
    }

    public async Task<Result<IList<InvestmentDto>>> GetByProject(ProjectId projectId)
    {
        var investments = await indexerService.GetInvestmentsAsync(projectId.Value);

        var investmentDtos = investments.Select(inv => new InvestmentDto
        {
            ProjectId = projectId,
            InvestorKey = inv.InvestorPublicKey,
            Amount = inv.TotalAmount,
            TransactionId = inv.TransactionId
        }).ToList();

        return investmentDtos;
    }
}
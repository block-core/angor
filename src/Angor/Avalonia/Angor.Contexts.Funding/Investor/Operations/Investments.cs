using System.Reactive.Linq;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Mixins;
using Zafiro.Reactive;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class Investments
{
    public record InvestmentsPortfolioRequest(Guid WalletId) : IRequest<Result<IEnumerable<InvestedProjectDto>>>;
    
    public class InvestmentsPortfolioHandler(ISeedwordsProvider seedwordsProvider,
        IDerivationOperations _DerivationOperations,
        IRelayService relayService,
        IEncryptionService _encryptionService,
        ISerializer serializer,
        IIndexerService indexerService,
        IInvestmentRepository investmentRepository,
        ISignService signService,
        INostrEncryption nostrEncryption
    ) : IRequestHandler<InvestmentsPortfolioRequest,Result<IEnumerable<InvestedProjectDto>>>
    {

        public async Task<Result<IEnumerable<InvestedProjectDto>>> Handle(InvestmentsPortfolioRequest request, CancellationToken cancellationToken)
        {
            var result = await RetrieveStorageCredentials(request);
            if (result.IsFailure)
                return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve storage credentials: " + result.Error);

            var investments = await investmentRepository.GetAllAsync(request.WalletId);

            IEnumerable<string> projectIds;
            
            if (investments.IsFailure || !investments.Value.Any()) // If no investments found, try to retrieve from relay
            {
                var investmentRecordsLookup =
                    await GetInvestmentRecordsFromRelayTask(result.Value.storageAccountKey, result.Value.password);

                if (investmentRecordsLookup.IsFailure)
                    return Result.Failure<IEnumerable<InvestedProjectDto>>("Failed to retrieve investment records: " +
                                                                           investmentRecordsLookup.Error);

                if (investmentRecordsLookup.Value.ProjectIdentifiers.Count == 0)
                    return Result.Success(Enumerable.Empty<InvestedProjectDto>());
                
                projectIds = investmentRecordsLookup.Value.ProjectIdentifiers.Select(x => x.ProjectIdentifier);
            }
            else
            {
                projectIds = investments.Value.Select(investment => investment.ProjectId.Value);    
            }
            //
            // var test = GatherAllProjectPublicDataPipeline(projectIds.ToArray())
            //     .SelectMany<IEnumerable<PipelineDto>, PipelineDto>(list => list.ToObservable())
            //     .Bind<PipelineDto, PipelineDto>(async dto =>
            //     {
            //         var investorKey = await GetProjectInvestorPubKey(dto.FounderKey, request.WalletId);
            //         var investment = await indexerService.GetInvestmentAsync(dto.Id, investorKey);
            //         dto.Investment = new Amount(investment?.TotalAmount ?? 0);
            //         return dto;
            //     });
                

            return await GatherAllProjectPublicDataPipeline(projectIds.ToArray())
                .Select(InvestedProjectDto (x) => x)
                .ToList()
                .Select(x => x.AsEnumerable())
                .ToResult();
        }

        private IObservable<PipelineDto> GatherAllProjectPublicDataPipeline(params string[] projectIds)
        {
            return LookupProjectCreationDataByIdentifiers(projectIds)
                .Select(data => new PipelineDto
                {
                    FounderKey = data.FounderKey,
                    Id = data.ProjectIdentifier,
                    ProjectInfoEventId = data.NostrEventId
                })
                .ToList()
                .SelectMany(pipelineDtoList => 
                    GetProjectInfoForEventIds(pipelineDtoList.Select(x => x.ProjectInfoEventId).ToArray())
                        .Select(x =>
                        {
                            var dto = pipelineDtoList.FirstOrDefault(p => p.Id == x.ProjectIdentifier);
                            dto.Target = new Amount(x.TargetAmount);
                            return dto;
                        })) //Get project info for event IDs
                .ToList()
                .SingleOrDefaultAsync() //Only one list of project infos
                .SelectMany(projectInfos => //Get profiles information
                    GetProfileForPublicKey(projectInfos.Select(result => result.NostrPubKey).ToArray())
                        .Select(result =>
                        {
                            var dto = projectInfos.FirstOrDefault(p => p.NostrPubKey == result.Value.pubKey);
                            
                            if (dto == null)
                                return Result.Failure<PipelineDto>("Project DTO not found for public key: " + result.Value.pubKey);
                            
                            dto.Description = result.Value.profile.About;
                            dto.Name = result.Value.profile.Name;
                            dto.LogoUri = Uri.TryCreate(result.Value.profile.Website, UriKind.RelativeOrAbsolute, out var uri)
                                ? uri
                                : null; //TODO handle invalid URIs

                            return dto;
                        }))
                .Bind<PipelineDto,PipelineDto>(async dto =>
                {
                    var stats = await indexerService.GetProjectStatsAsync(dto.Id); // Get project stats for the project ID
                    dto.Raised = new Amount(stats.stats?.AmountInvested ?? 0);
                    dto.InRecovery = new Amount(stats.stats?.AmountInPenalties ?? 0);
                    return dto; // Return the updated InvestedProjectDto with stats
                })
                .Where(x => x.IsSuccess)
                .Select(PipelineDto (x) => x.Value) // Convert PipelineDto to InvestedProjectDto
;
        }

        private async Task<string> GetProjectInvestorPubKey(string founderPubkey, Guid walletId)
        {
            var words = await seedwordsProvider.GetSensitiveData(walletId);
            return _DerivationOperations.DeriveInvestorKey(words.Value.ToWalletWords(),founderPubkey);;
        }

        private class PipelineDto : InvestedProjectDto
        {
            public string FounderKey { get; set; }
            public string NostrPubKey { get; set; }
            public string ProjectInfoEventId { get; set; }
        }
        
        private IObservable<ProjectIndexerData> LookupProjectCreationDataByIdentifiers(params string[] projectIdentifiers)
        {
            return projectIdentifiers
                .ToObservable()
                .ToResult()
                .Bind(projectId => Result.Try(() => indexerService.GetProjectByIdAsync(projectId)))
                .Where(x => x.IsSuccess)
                .Select(x => x.Value);
        }

        private async Task<Result<(string storageAccountKey, string password)>> RetrieveStorageCredentials(InvestmentsPortfolioRequest request)
        {
            var words = await seedwordsProvider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
            {
                return Result.Failure<(string, string)>("Failed to retrieve sensitive data: " + words.Error);
            }
            var storageAccountKey = _DerivationOperations.DeriveNostrStoragePubKeyHex(words.Value.ToWalletWords());
            var password = _DerivationOperations.DeriveNostrStoragePassword(words.Value.ToWalletWords());
            
            return (storageAccountKey, password);
        }

        private Task<Result<InvestmentRecords>> GetInvestmentRecordsFromRelayTask(string storageAccountKey,
            string password)
        {
            var tcs = new TaskCompletionSource<Result<InvestmentRecords>>();
            
            relayService.LookupDirectMessagesForPubKey(storageAccountKey, null, 1, async (nostrEvent) =>
            {
                try
                {
                    var decrypted = await _encryptionService.DecryptData(nostrEvent.Content, password);
                    var investmentRecords = serializer.Deserialize<InvestmentRecords>(decrypted);
                    tcs.SetResult(investmentRecords);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }

            }, new[] { storageAccountKey });

            return tcs.Task;
        }

        private IObservable<ProjectInfo> GetProjectInfoForEventIds(params string[] eventIds)
        {
            return Observable.Create<ProjectInfo>(observable =>
            {
                var tcs = new TaskCompletionSource<bool>();
                
                relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
                    observable.OnNext, () =>
                    {
                        observable.OnCompleted();
                        tcs.SetResult(true);
                    }, eventIds);

                return tcs.Task;
            });
        }
        
        private IObservable<Result<(string pubKey,ProjectMetadata profile)>> GetProfileForPublicKey(params string[] publicKey)
        {
            return Observable.Create<Result<(string,ProjectMetadata)>>(observable =>
            {
                var tcs = new TaskCompletionSource<bool>();
                
                relayService.LookupNostrProfileForNPub(
                    (x,y) =>
                        Result.Try(() => observable.OnNext((x, y))),
                    () =>
                    {
                        observable.OnCompleted();
                        tcs.SetResult(true);
                    },
                    publicKey);
                
                return tcs.Task;
            });
        }
        
        private IObservable<SignatureInfo> GetFounderSignatures(string investorKey, string publicKey, DateTime createdAt, string eventId)
        {
            return Observable.Create<SignatureInfo>(observable =>
            {
                var tcs = new TaskCompletionSource<bool>();

                signService.GetInvestmentRequestApproval(investorKey, publicKey, createdAt, eventId, async content =>
                {
                    try
                    {
                        var s = await nostrEncryption.Nip4Decryption<SignatureInfo>(content, investorKey,
                            publicKey);

                        observable.OnNext(s);
                        observable.OnCompleted();
                        tcs.SetResult(true);
                    }
                    catch (Exception e)
                    {
                        observable.OnError(e);
                        tcs.SetResult(false);
                    }
                });
                
                return tcs.Task;
            });
        }
    }
}




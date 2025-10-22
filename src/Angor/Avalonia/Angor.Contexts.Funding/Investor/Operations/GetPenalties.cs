using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using MediatR;
using Zafiro.CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Investor.Operations;

public class GetPenalties
{
    public record GetPenaltiesRequest(Guid WalletId) : IRequest<Result<IEnumerable<PenaltiesDto>>>;

    public class GetPenaltiesHandler(
        IPortfolioRepository investmentRepository,
        IIndexerService indexerService,
        IRelayService relayService,
        ITransactionRepository transactionRepository,
        IProjectInvestmentsService investmentsService)
        : IRequestHandler<GetPenaltiesRequest, Result<IEnumerable<PenaltiesDto>>>
    {

        public async Task<Result<IEnumerable<PenaltiesDto>>> Handle(GetPenaltiesRequest request,
            CancellationToken cancellationToken)
        {
            var penaltyProjects = await FetchInvestedProjects(request.WalletId);

            if (penaltyProjects.IsFailure)
                return Result.Failure<IEnumerable<PenaltiesDto>>(penaltyProjects.Error);

            var penaltyList = penaltyProjects.Value.ToList();
            
            await RefreshPenalties(penaltyList);

            return Result.Success(penaltyList.ToList().Select(p => new PenaltiesDto
            {
                ProjectIdentifier = p.ProjectIdentifier,
                InvestorPubKey = p.InvestorPubKey,
                AmountInRecovery = p.AmountInRecovery,
                TotalAmountSats = p.TotalAmountSats,
                IsExpired = p.IsExpired,
                DaysLeftForPenalty = p.DaysLeftForPenalty,
                
                //TODO do we want to send the ids so the user can view on explorers?
                // TransactionId = p.TransactionId,
                // EndOfProjectTransactionId = p.EndOfProjectTransactionId,
                // RecoveryTransactionId = p.RecoveryTransactionId,
                // RecoveryReleaseTransactionId = p.RecoveryReleaseTransactionId,
                // UnfundedReleaseTransactionId = p.UnfundedReleaseTransactionId,
            }));
        }

        public async Task<Result<IEnumerable<LookupInvestment>>> FetchInvestedProjects(Guid walletId)
        {
            var investments = await investmentRepository.GetByWalletId(walletId);

            if (investments.IsFailure)
                return Result.Failure<IEnumerable<LookupInvestment>>(investments.Error);

            if (investments.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(Enumerable.Empty<LookupInvestment>());

            return await Result.Try(() => investments.Value.ProjectIdentifiers //lookup investments pipeline
                .ToObservable()
                .Select(x => //get the investment
                    Result.Try(() => indexerService.GetInvestmentAsync(x.ProjectIdentifier, x.InvestorPubKey))
                        .Map<ProjectInvestment?, LookupInvestment>(projectInvestment => new LookupInvestment
                        {
                            ProjectIdentifier = x.ProjectIdentifier,
                            InvestorPubKey = x.InvestorPubKey,
                            TransactionId = projectInvestment!.TransactionId,
                            TotalAmountSats = projectInvestment.TotalAmount,
                        }))
                .Merge()
                .Bind(investment => //Get the project by id
                    Result.Try(() => indexerService.GetProjectByIdAsync(investment.ProjectIdentifier))
                        .Map(project =>
                        {
                            investment.NostrEventId = project.NostrEventId;
                            return investment;
                        }))
                 .Where(x => x.IsSuccess)
                .ToList() // merge to a list and get the project info for all items in the list
                .Select(list =>
                {
                    var projectInfos = ProjectInfos(list.Select(item => item.Value.NostrEventId).ToArray())
                        .ToEnumerable()
                        .ToList(); // Collect emitted items before timeout

                    foreach (var info in projectInfos)
                    {
                        var lookupInvestment =
                            list.FirstOrDefault(item => item.Value.ProjectIdentifier == info.ProjectIdentifier);
                        if (lookupInvestment.Value != null)
                            lookupInvestment.Value.ProjectInfo = info;
                    }
                    return list; // Return the updated list even if ProjectInfos times out
                })
                .SelectMany(x => x) // Flatten the list of LookupInvestment
                .Bind(x => investmentsService.ScanInvestmentSpends(x.ProjectInfo,x.TransactionId)
                    .Map(response =>
                    {
                        x.EndOfProjectTransactionId = response.EndOfProjectTransactionId;
                        x.RecoveryTransactionId = response.RecoveryTransactionId;
                        x.AmountInRecovery = response.AmountInRecovery;
                        x.RecoveryReleaseTransactionId = response.RecoveryReleaseTransactionId;
                        x.UnfundedReleaseTransactionId = response.UnfundedReleaseTransactionId;
                        return x;
                    }))
                .Where(x => x.IsSuccess && x.Value.RecoveryTransactionId != null) // Filter out those without recovery transaction
                .Select(x => x.Value)
                .ToList()
                .Select(x => x.AsEnumerable())
                .ToTask());
        }

        
        private IObservable<ProjectInfo> ProjectInfos(IEnumerable<string> eventIds)
        {
            return Observable.Create<ProjectInfo>(observer =>
                {
                    relayService.LookupProjectsInfoByEventIds<ProjectInfo>(
                        observer.OnNext,
                        observer.OnCompleted,
                        eventIds.ToArray()
                    );

                    return Disposable.Empty;
                }).Timeout(TimeSpan.FromSeconds(30))
                .Catch<ProjectInfo, Exception>(ex => Observable.Empty<ProjectInfo>());
        
        }

        // This will be part of the recover operation - might move to another service
        public async Task<Result<string>> RefreshPenalties(IEnumerable<LookupInvestment> penaltyProjects)
        {
            try
            {
                foreach (var penaltyProject in penaltyProjects)
                {
                    var recoveryTransaction = await transactionRepository.GetTransactionInfoByIdAsync(penaltyProject.RecoveryTransactionId);

                    var totalsats = recoveryTransaction.Outputs
                        .Where(s => Script.FromHex(s.ScriptPubKey).IsScriptType(ScriptType.P2WSH)).Sum(s => s.Balance);
                    penaltyProject.TotalAmountSats = totalsats;

                    if (penaltyProject.ProjectInfo == null)
                    {
                        // If the project info is not available, we cannot calculate the penalty days
                        penaltyProject.DaysLeftForPenalty = 365; //We set to maximum days
                        penaltyProject.IsExpired = false;
                        continue;
                    }
                    
                    var expieryDate = Utils.UnixTimeToDateTime(recoveryTransaction.Timestamp)
                        .AddDays(penaltyProject.ProjectInfo.PenaltyDays);
                    penaltyProject.DaysLeftForPenalty = (expieryDate.Date - DateTimeOffset.UtcNow.Date).Days;
                    penaltyProject.IsExpired = (expieryDate - DateTimeOffset.UtcNow).Days <= 0;
                }
            }
            catch (Exception e)
            {
                return Result.Failure<string>(e.Message);
            }

            return Result.Success("");
        }
    }
}

public class LookupInvestment
{
    public string ProjectIdentifier { get; set; }
    public string InvestorPubKey { get; set; }
    public string NostrEventId { get; set; }
    public string TransactionId { get; set; }
    public string EndOfProjectTransactionId { get; set; }
    public string RecoveryTransactionId { get; set; }
    public long AmountInRecovery { get; set; }
    public string RecoveryReleaseTransactionId { get; set; }
    public string UnfundedReleaseTransactionId { get; set; }
    public ProjectInfo ProjectInfo { get; set; }
    
    public long TotalAmountSats { get; set; }
    public bool IsExpired { get; set; }
    public int DaysLeftForPenalty { get; set; }
}
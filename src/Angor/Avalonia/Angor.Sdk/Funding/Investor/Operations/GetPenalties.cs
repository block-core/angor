using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor.Operations;

public class GetPenalties
{
    public record GetPenaltiesRequest(WalletId WalletId) : IRequest<Result<GetPenaltiesResponse>>;

    public record GetPenaltiesResponse(IEnumerable<PenaltiesDto> Penalties);

    public class GetPenaltiesHandler(
        IPortfolioService investmentService,
        IAngorIndexerService angorIndexerService,
        ITransactionService transactionService,
        IProjectInvestmentsService investmentsService,
        IProjectService projectService) : IRequestHandler<GetPenaltiesRequest, Result<GetPenaltiesResponse>>
    {

        public async Task<Result<GetPenaltiesResponse>> Handle(GetPenaltiesRequest request,
            CancellationToken cancellationToken)
        {
            var penaltyProjects = await FetchInvestedProjects(request.WalletId.Value);

            if (penaltyProjects.IsFailure)
                return Result.Failure<GetPenaltiesResponse>(penaltyProjects.Error);

            var penaltyList = penaltyProjects.Value.ToList();

            penaltyList.Reverse(); // Show the most recent penalties first
            
            var penaltyResult = await RefreshPenalties(penaltyList);

            if (penaltyResult.IsFailure)
                return Result.Failure<GetPenaltiesResponse>(penaltyResult.Error);
            
            var penalties = penaltyList.ToList().Select(p => new PenaltiesDto
            {
                ProjectIdentifier = p.ProjectIdentifier,
                InvestorPubKey = p.InvestorPubKey,
                AmountInRecovery = p.AmountInRecovery,
                TotalAmountSats = p.TotalAmountSats,
                IsExpired = p.IsExpired,
                DaysLeftForPenalty = p.DaysLeftForPenalty,
                ProjectName = p.Project?.Name
                
                //TODO do we want to send the ids so the user can view on explorers?
                // TransactionId = p.TransactionId,
                // EndOfProjectTransactionId = p.EndOfProjectTransactionId,
                // RecoveryTransactionId = p.RecoveryTransactionId,
                // RecoveryReleaseTransactionId = p.RecoveryReleaseTransactionId,
                // UnfundedReleaseTransactionId = p.UnfundedReleaseTransactionId,
            });
            
            return Result.Success(new GetPenaltiesResponse(penalties));
        }

        private async Task<Result<IEnumerable<LookupInvestment>>> FetchInvestedProjects(string walletId)
        {
            var investments = await investmentService.GetByWalletId(walletId);

            if (investments.IsFailure)
                return Result.Failure<IEnumerable<LookupInvestment>>(investments.Error);

            if (investments.Value.ProjectIdentifiers.Count == 0)
                return Result.Success(Enumerable.Empty<LookupInvestment>());

            var investmentTasks = investments.Value.ProjectIdentifiers
                                             .Select(x => 
                                                         angorIndexerService.GetInvestmentAsync(x.ProjectIdentifier, x.InvestorPubKey))
                                             .ToList();

            var projectsTask = projectService.GetAllAsync(
                investments.Value.ProjectIdentifiers.Select(x => new ProjectId(x.ProjectIdentifier)).ToArray());

            var investmentsDictionary = investments.Value.ProjectIdentifiers.ToDictionary(i => i.InvestorPubKey);
            
            await Task.WhenAll(investmentTasks);

            var lookups = investmentTasks
                          .Where(t => t is { IsCompletedSuccessfully: true, Result: not null })
                          .Select(t =>
                          {
                              var investment = investmentsDictionary[t.Result!.InvestorPublicKey];

                              return new LookupInvestment
                              {
                                  ProjectIdentifier = investment.ProjectIdentifier,
                                  InvestorPubKey = investment.InvestorPubKey,
                                  TransactionId = t.Result!.TransactionId,
                                  TotalAmountSats = t.Result!.TotalAmount,
                              };
                          }).ToList();

            await projectsTask; // Make sure we have all the projects in memory
            
            if (projectsTask.Result.IsFailure)
                return Result.Failure<IEnumerable<LookupInvestment>>(projectsTask.Result.Error);

            var projectsDict = projectsTask.Result.Value.ToDictionary(p => p.Id.Value);
            
            var scanTasks = lookups
                            .Where(l => projectsDict.ContainsKey(l.ProjectIdentifier))
                            .Select(l =>
                                        investmentsService.ScanInvestmentSpends(projectsDict[l.ProjectIdentifier].ToProjectInfo(), l.TransactionId))
                            .ToList();

            await Task.WhenAll(scanTasks);

            foreach (var scanTask in scanTasks.Where(t => t.Result.IsSuccess))
            {
                var response = scanTask.Result.Value;
                var lookup = lookups.First(l => l.TransactionId == response.TransactionId);

                lookup.AmountInRecovery = response.AmountInRecovery;
                lookup.EndOfProjectTransactionId = response.EndOfProjectTransactionId;
                lookup.RecoveryTransactionId = response.RecoveryTransactionId;
                lookup.RecoveryReleaseTransactionId = response.RecoveryReleaseTransactionId;
                lookup.UnfundedReleaseTransactionId = response.UnfundedReleaseTransactionId;
            }

            return Result.Success(lookups.Where(x => !string.IsNullOrEmpty(x.RecoveryTransactionId)).AsEnumerable());
        }


        // This will be part of the recover operation - might move to another service
        public async Task<Result<string>> RefreshPenalties(IEnumerable<LookupInvestment> penaltyProjects)
        {
            try
            {
                foreach (var penaltyProject in penaltyProjects)
                {
                    var recoveryTransaction = await transactionService.GetTransactionInfoByIdAsync(penaltyProject.RecoveryTransactionId);

                    var totalsats = recoveryTransaction.Outputs
                        .Where(s => Script.FromHex(s.ScriptPubKey).IsScriptType(ScriptType.P2WSH)).Sum(s => s.Balance);
                    penaltyProject.TotalAmountSats = totalsats;

                    if (penaltyProject.Project == null)
                    {
                        // If the project info is not available, we cannot calculate the penalty days
                        penaltyProject.DaysLeftForPenalty = 365; //We set to maximum days
                        penaltyProject.IsExpired = false;
                        continue;
                    }
                    
                    var expieryDate = Utils.UnixTimeToDateTime(recoveryTransaction.Timestamp)
                        .AddDays(penaltyProject.Project.PenaltyDuration.Days);
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
    public Project? Project { get; set; }
    
    public long TotalAmountSats { get; set; }
    public bool IsExpired { get; set; }
    public int DaysLeftForPenalty { get; set; }
}
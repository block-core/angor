using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Projects.Operations;

public static class ProjectStatistics
{
    public record ProjectStatsRequest(ProjectId ProjectId) : IRequest<Result<ProjectStatisticsDto>>;
    
    public class ProjectStatsHandler(
        IIndexerService indexerService,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions,
        IProjectRepository projectRepository) : IRequestHandler<ProjectStatsRequest, Result<ProjectStatisticsDto>>
    {
        public async Task<Result<ProjectStatisticsDto>> Handle(ProjectStatsRequest request, CancellationToken cancellationToken)
        {
            var stagesInformation = await ScanFullInvestments(request.ProjectId.Value);
            
            if (stagesInformation.IsFailure)
            {
                return Result.Failure<ProjectStatisticsDto>(stagesInformation.Error);
            }
            
            // TODO Jose: protect against exceptions until the method handles nulls properly
            return Result.Try(() => CalculateTotalValues(stagesInformation.Value.ToList()));
        }
        
        private static ProjectStatisticsDto CalculateTotalValues(List<StageData> stagesInformation)
        {
            var nextStage = stagesInformation.Where(stage => stage.Stage.ReleaseDate > DateTime.UtcNow)
                .OrderBy(stage => stage.Stage.ReleaseDate)
                .FirstOrDefault();

            var currentStage = stagesInformation.OrderBy(x => x.StageIndex).LastOrDefault(stage => stage.Stage.ReleaseDate <= DateTime.UtcNow);
            
            var dto = new ProjectStatisticsDto
            {
                NextStage = new NextStageDto()
                {
                    // TODO Jose: Handle the case when there is no current stage
                    // currentStage can be null!
                    PercentageToRelease = currentStage.Stage.AmountToRelease, ReleaseDate = currentStage.Stage.ReleaseDate,
                    DaysUntilRelease = nextStage != null ? (nextStage.Stage.ReleaseDate - DateTime.UtcNow).Days : 0,
                    StageIndex = nextStage != null ? stagesInformation.IndexOf(nextStage) : stagesInformation.Count - 1,
                },
                TotalStages = stagesInformation.Count != 0 ? stagesInformation.Count : 1,
            };

            foreach (var stage in stagesInformation)
            {
                var totalStageTransactions = stage.Items.Count();
                var investedAmount = stage.Items.Sum(c => c.Amount);
                var availableInvestedAmount = stage.Items.Where(c => !c.IsSpent).Sum(c => c.Amount);
                var spentStageAmount = stage.Items.Where(c => c.IsSpent).Sum(c => c.Amount);
                var spentStageTransactions = stage.Items.Count(c => c.IsSpent);
                var daysUntilRelease = (stage.Stage.ReleaseDate - DateTime.UtcNow).Days;

                dto.TotalInvested += investedAmount;
                dto.AvailableBalance += availableInvestedAmount;
                dto.TotalTransactions += totalStageTransactions;
                dto.SpentAmount += spentStageAmount;
                dto.SpentTransactions += spentStageTransactions;

                if (daysUntilRelease <= 0)
                {
                    dto.WithdrawableAmount += availableInvestedAmount;
                }
            }
            return dto;
        }
        
        private async Task<Result<IEnumerable<StageData>>> ScanFullInvestments(string projectId)
        {
            var project = await projectRepository.Get(new ProjectId(projectId));

            if (project.IsFailure)
                return Result.Failure<IEnumerable<StageData>>("Failed to retrieve project data.");
            
            var network = networkConfiguration.GetNetwork();

            var investments = await indexerService.GetInvestmentsAsync(projectId)
                .ToObservable()
                .SelectMany(trxsList => trxsList.ToObservable()) //Flatten the list of transactions and look at the task result
                .Select(x => indexerService.GetTransactionHexByIdAsync(x.TransactionId))
                .Select(hex => network.CreateTransaction(hex.Result))
                .Select(trx => indexerService.GetTransactionInfoByIdAsync(trx.GetHash().ToString())
                    .ToObservable()
                    .Select(trxInfo => (trx, trxInfo)))
                .Merge()
                .ToList();

            var stageDataList = await project.Value.Stages
                .Select(async x => new StageData
                {
                    Stage = new Stage { ReleaseDate = x.ReleaseDate, AmountToRelease = x.RatioOfTotal },
                    StageIndex = x.Index,
                    Items = await investments.Select(tuple =>
                            CheckSpentFund(tuple.trxInfo.Outputs
                                        .First(outp => outp.Index == x.Index + 2), // +2 because the first two outputs are the Angor fee and Op return output
                                    tuple.trx, project.Value.ToProjectInfo(),
                                    x.Index)
                                .ToObservable() 
                        )
                        .Merge()
                        .Where(result => result.IsSuccess)
                        .Select(x => x.Value)
                        .ToList()
                        .ToTask()
                })
                .ToObservable()
                .Merge()
                .ToList()
                .Select(x => x.AsEnumerable());
            
            return Result.Success(stageDataList); ;
        }
        
        
          private async Task<Result<StageDataTrx>> CheckSpentFund(QueryTransactionOutput output, Transaction investmentTransaction, ProjectInfo projectInfo,
            int stageIndex)
          {
              var network = networkConfiguration.GetNetwork();
              
              var stageIndexInTransaction = stageIndex + 2;
              
              var item = new StageDataTrx
              {
                  Trxid = investmentTransaction.GetHash().ToString(),
                  Outputindex = stageIndex,
                  OutputAddress = investmentTransaction.Outputs[stageIndexInTransaction].ScriptPubKey.WitHash.GetAddress(network).ToString(),
                  Amount = investmentTransaction.Outputs[stageIndexInTransaction].Value.Satoshi
              };
              
            if (!string.IsNullOrEmpty(output.SpentInTransaction))
            {
                item.IsSpent = true;

                //TODO handle unconfirmed transactions
                // updateUnconfirmedOutbound |=
                //     unconfirmedOutbound.TryRemoveOutpoint(new Outpoint(item.Trxid, item.Outputindex));

                // try to resolve the destination
                var spentInTransaction =
                    await indexerService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

                var input = spentInTransaction?.Inputs.FirstOrDefault(input =>
                    input.InputTransactionId == item.Trxid && input.InputIndex == item.Outputindex);

                if (input != null && investmentTransaction != null)
                {
                    item.ProjectScriptType = investorTransactionActions.DiscoverUsedScript(projectInfo,
                        investmentTransaction, stageIndex, input.WitScript);

                    switch (item.ProjectScriptType.ScriptType)
                    {
                        case ProjectScriptTypeEnum.Founder:
                        {
                            item.SpentType = "founder";
                            break;
                        }
                        case ProjectScriptTypeEnum.InvestorWithPenalty:
                        case ProjectScriptTypeEnum.EndOfProject:
                        case ProjectScriptTypeEnum.InvestorNoPenalty:
                        {
                            item.SpentType = "investor";
                            break;
                        }
                    }
                }

                return item;
            }

            // item.IsSpent = unconfirmedOutbound.ContainsOutpoint(new Outpoint(item.Trxid, item.Outputindex));
            if (item.IsSpent)
            {
                // the trx is unconfirmed, we wait till
                // it gets confirmed to discover who spent it
                item.SpentType = "pending";
            }

            return item;
          }
          
        public class StageData
        {
            public int StageIndex;
            public Stage Stage;
            public IList<StageDataTrx> Items = new List<StageDataTrx>();
        }

        public class StageDataTrx
        {
            public string Trxid;
            public int Outputindex;
            public string OutputAddress;
            public long Amount;
            public bool IsSpent;
            public string SpentType;  // "founder" or "investor"
            public string InvestorNpub;  // Optional, can be null
            public ProjectScriptType ProjectScriptType;
        }
        
    }
}
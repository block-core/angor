using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Shared.Protocol;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectInvestmentsRepository(IProjectRepository projectRepository, INetworkConfiguration networkConfiguration,
    IIndexerService indexerService, IInvestorTransactionActions investorTransactionActions) : IProjectInvestmentsRepository
{
    public async Task<Result<IEnumerable<StageData>>> ScanFullInvestments(string projectId)
    {
        var project = await projectRepository.Get(new ProjectId(projectId));

            if (project.IsFailure)
                return Result.Failure<IEnumerable<StageData>>("Failed to retrieve project data.");
            
            var network = networkConfiguration.GetNetwork();

            try
            {
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
                        Stage = new Stage() { ReleaseDate = x.ReleaseDate, AmountToRelease = x.RatioOfTotal },
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
                
                return Result.Success(stageDataList);
            }
            catch (Exception e)
            {
                //TODO add logging
                return Result.Failure<IEnumerable<StageData>>(e.Message);
            }
    }

    public async Task<Result<StageDataTrx>> CheckSpentFund(QueryTransactionOutput output, Transaction investmentTransaction, ProjectInfo projectInfo,
        int stageIndex)
    {
        var network = networkConfiguration.GetNetwork();
              
              var stageIndexInTransaction = stageIndex + 2;
              
              var item = new StageDataTrx
              {
                  Trxid = investmentTransaction.GetHash().ToString(),
                  Outputindex = stageIndexInTransaction,
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
}
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Shared;
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
            
            try
            {
                var projectInvestments = await indexerService.GetInvestmentsAsync(projectId);
                
                var (_, isFailure, investments, error) = await GetProjectInvestments(projectInvestments);
                if (isFailure)
                    return Result.Failure<IEnumerable<StageData>>("Failed to retrieve investment transactions: " + error);

                var stageDataList = await project.Value.Stages
                    .Select(async x => new StageData
                    {
                        Stage = new Stage() { ReleaseDate = x.ReleaseDate, AmountToRelease = x.RatioOfTotal },
                        StageIndex = x.Index,
                        Items = await investments.Select(tuple => CheckSpentFund(tuple.trxInfo?.Outputs.First(outp
                                        => outp.Index == x.Index + 2) ?? null,
                                    // +2 because the first two outputs are the Angor fee and Op return output but the stages start from index 1
                                    tuple.trx, project.Value.ToProjectInfo(), x.Index)
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
                    .Select(stageDataTrx => //We need to add the investor public key to the result
                    {
                        foreach (var item in stageDataTrx.Items)
                            item.InvestorPublicKey = projectInvestments
                                .First(p => p.TransactionId == item.Trxid)
                                .InvestorPublicKey;

                        return stageDataTrx;
                    })
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

    private Task<Result<IList<(Transaction trx, QueryTransaction? trxInfo)>>> GetProjectInvestments(List<ProjectInvestment> projectInvestments)
    {
        var network = networkConfiguration.GetNetwork();

        return Result.Try(() => projectInvestments
            .ToObservable()
            .Select(x => indexerService.GetTransactionHexByIdAsync(x.TransactionId)) // get the hex of the transaction
            .Select(hex => network.CreateTransaction(hex.Result)) // parse the transaction
            .Select(trx => indexerService
                .GetTransactionInfoByIdAsync(trx.GetHash().ToString()) // get the transaction info
                .ToObservable()
                .Select(trxInfo => (trx, trxInfo))) // return both
            .Merge()
            .ToList()
            .ToTask());
    }

    public async Task<Result<StageDataTrx>> CheckSpentFund(QueryTransactionOutput? output,
        Transaction investmentTransaction, ProjectInfo projectInfo,
        int stageIndex)
    {
        if (output == null)
            return Result.Failure<StageDataTrx>("Output not found");

        var network = networkConfiguration.GetNetwork();

        var stageIndexInTransaction = stageIndex + 2;

        var txOut = investmentTransaction.Outputs[stageIndexInTransaction];

        var item = new StageDataTrx
        {
            Trxid = investmentTransaction.GetHash().ToString(),
            Outputindex = stageIndexInTransaction,
            OutputAddress = txOut.ScriptPubKey.WitHash.GetAddress(network).ToString(),
            Amount = txOut.Value.Satoshi
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
    
    public async Task<Result<InvestmentSpendingLookup>> ScanInvestmentSpends(ProjectInfo project, string transactionId)
        {
            var trxInfo = await indexerService.GetTransactionInfoByIdAsync(transactionId);

            if (trxInfo == null)
                return Result.Failure<InvestmentSpendingLookup>("Transaction not found");

            var trxHex = await indexerService.GetTransactionHexByIdAsync(transactionId);
            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(trxHex);

            var response = new InvestmentSpendingLookup
            {
                TransactionId = transactionId,
                ProjectIdentifier = project.ProjectIdentifier
            };
            
            for (int stageIndex = 0; stageIndex < project.Stages.Count; stageIndex++)
            {
                var output = trxInfo.Outputs.First(f => f.Index == stageIndex + 2);

                if (!string.IsNullOrEmpty(output.SpentInTransaction))
                {
                    var spentInfo = await indexerService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

                    if (spentInfo == null)
                        continue;

                    var spentInput = spentInfo.Inputs.FirstOrDefault(input =>
                        (input.InputTransactionId == transactionId) &&
                        (input.InputIndex == output.Index));

                    if (spentInput != null) //TODO move the script discovery to another class
                    {
                        var scriptType = investorTransactionActions.DiscoverUsedScript(project,
                            investmentTransaction, stageIndex, spentInput.WitScript);

                        switch (scriptType.ScriptType)
                        {
                            case ProjectScriptTypeEnum.Founder:
                            {
                                // check the next stage
                                continue;
                            }

                            case ProjectScriptTypeEnum.EndOfProject:
                            {
                                response.EndOfProjectTransactionId = output.SpentInTransaction;
                                return response;
                            }

                            case ProjectScriptTypeEnum.InvestorWithPenalty:
                            {
                                response.RecoveryTransactionId = output.SpentInTransaction;
                                var totalsats = trxInfo.Outputs.SkipLast(1).Sum(s => s.Balance);
                                response.AmountInRecovery = totalsats;

                                var spentRecoveryInfo =
                                    await indexerService.GetTransactionInfoByIdAsync(response
                                        .RecoveryTransactionId);

                                if (spentRecoveryInfo == null) 
                                    return response;
                                
                                if (spentRecoveryInfo.Outputs.SkipLast(1)
                                    .Any(_ => !string.IsNullOrEmpty(_.SpentInTransaction)))
                                {
                                    response.RecoveryReleaseTransactionId = spentRecoveryInfo.Outputs
                                        .First(_ => !string.IsNullOrEmpty(_.SpentInTransaction)).SpentInTransaction;
                                }

                                return response;
                            }

                            case ProjectScriptTypeEnum.InvestorNoPenalty:
                            {
                                response.UnfundedReleaseTransactionId = output.SpentInTransaction;
                                return response;
                            }
                        }
                    }
                }
            }

            return response;
        }
}
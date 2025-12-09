using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Protocol;
using Angor.Shared.Utilities;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class ProjectInvestmentsService(IProjectService projectService, INetworkConfiguration networkConfiguration,
    IAngorIndexerService angorIndexerService, IInvestorTransactionActions investorTransactionActions,
    ITransactionService transactionService) : IProjectInvestmentsService
{
    public async Task<Result<IEnumerable<StageData>>> ScanFullInvestments(string projectId)
    {
        var project = await projectService.GetAsync(new ProjectId(projectId));

        if (project.IsFailure)
            return Result.Failure<IEnumerable<StageData>>("Failed to retrieve project data.");

        try
        {
            var projectInvestments = await angorIndexerService.GetInvestmentsAsync(projectId);

            if (projectInvestments.Count == 0)
                return Result.Success(new List<StageData>().AsEnumerable());

            // Handle based on project type
            return project.Value.ProjectType switch
            {
                ProjectType.Invest => await ScanInvestTypeInvestments(project.Value, projectInvestments),
                ProjectType.Fund or ProjectType.Subscribe => await ScanDynamicTypeInvestments(project.Value, projectInvestments),
                _ => Result.Failure<IEnumerable<StageData>>("Unknown project type")
            };
        }
        catch (Exception e)
        {
            //TODO add logging
            return Result.Failure<IEnumerable<StageData>>(e.Message);
        }
    }

    private async Task<Result<IEnumerable<StageData>>> ScanInvestTypeInvestments(Project project, List<ProjectInvestment> projectInvestments)
    {
        var stageDataList = project.Stages
            .Select(x => new StageData
            {
                Stage = new Stage() { ReleaseDate = x.ReleaseDate, AmountToRelease = x.RatioOfTotal },
                StageIndex = x.Index,
                Items = [],
                IsDynamic = false // Invest projects have fixed stages
            }).ToList();

        var investmentsResult = await GetProjectInvestmentsTransactionsAsync(projectInvestments);

        if (investmentsResult.IsFailure)
            return Result.Failure<IEnumerable<StageData>>("Failed to retrieve investment transactions: " + investmentsResult.Error);

        foreach (var stage in stageDataList)
        {
            var tasks = investmentsResult.Value.Select(tuple =>
                (output: tuple.trxInfo?.Outputs.First(outp => outp.Index == stage.StageIndex + 2)
                ?? null,
                 transaction: tuple.trx, index: stage.StageIndex))
                .Select(x => CheckSpentFund(x.output, x.transaction, project.ToProjectInfo(), x.index));

            var results = await Task.WhenAll(tasks);

            var combinedResult = results.Combine();

            if (combinedResult.IsFailure)
                return Result.Failure<IEnumerable<StageData>>("Failed to process investment transactions: " +
                                                              combinedResult.Error);

            stage.Items = combinedResult.Value.ToList();

            foreach (var item in stage.Items)
            {
                item.InvestorPublicKey = projectInvestments
                    .First(p => p.TransactionId == item.Trxid)
                    .InvestorPublicKey;
            }
        }

        return Result.Success(stageDataList.AsEnumerable());
    }

    private async Task<Result<IEnumerable<StageData>>> ScanDynamicTypeInvestments(Project project, List<ProjectInvestment> projectInvestments)
    {
        var investmentsResult = await GetProjectInvestmentsTransactionsAsync(projectInvestments);

        if (investmentsResult.IsFailure)
            return Result.Failure<IEnumerable<StageData>>("Failed to retrieve investment transactions: " + investmentsResult.Error);

        var stageGroups = new Dictionary<(int index, DateTime releaseDate), List<(StageDataTrx trx, string investorPubKey)>>();

        foreach (var (trx, trxInfo) in investmentsResult.Value)
        {
            if (trxInfo == null)
                continue;

            var investment = projectInvestments.FirstOrDefault(p => p.TransactionId == trx.GetHash().ToString());
            if (investment == null)
                continue;

            var fundingParams = FundingParameters.CreateFromTransaction(project.ToProjectInfo(), trx);

            if (fundingParams.InvestmentStartDate == null || fundingParams.PatternIndex >= project.DynamicStagePatterns.Count)
                continue;

            var pattern = project.DynamicStagePatterns[fundingParams.PatternIndex];
            var stageCount = fundingParams.StageCountOverride ?? pattern.StageCount;

            // Calculate percentage per stage for this investment (equal split)
            var percentagePerStage = 100m / stageCount;

            var taprootOutputs = trx.Outputs.AsIndexedOutputs()
                .Where(txout => txout.TxOut.ScriptPubKey.IsTaprooOutput())
                .Select(_ => _.TxOut)
                .ToArray();

            for (int stageIndex = 0; stageIndex < Math.Min(stageCount, taprootOutputs.Length); stageIndex++)
            {
                var output = taprootOutputs[stageIndex];

                var qouts = trxInfo.Outputs.ElementAt(stageIndex);

                var releaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(
                    fundingParams.InvestmentStartDate.Value,
                    pattern,
                    stageIndex);

                var stageDataResult = await CheckSpentFund(qouts, trx, project.ToProjectInfo(), stageIndex);

                if (stageDataResult.IsFailure)
                    continue;

                var stageData = stageDataResult.Value;
                stageData.InvestorPublicKey = investment.InvestorPublicKey;
                stageData.DynamicReleaseDate = releaseDate;
                stageData.PatternIndex = fundingParams.PatternIndex;
                stageData.InvestmentStartDate = fundingParams.InvestmentStartDate;
                stageData.StageIndex = stageIndex;
                stageData.AmountPercentage = percentagePerStage;

                var key = (stageIndex, releaseDate.Date);
                if (!stageGroups.ContainsKey(key))
                {
                    stageGroups[key] = new List<(StageDataTrx, string)>();
                }

                stageGroups[key].Add((stageData, investment.InvestorPublicKey));
            }
        }

        var stageDataList = stageGroups
                .OrderBy(kvp => kvp.Key.releaseDate)
                .ThenBy(kvp => kvp.Key.index)
                .Select(kvp =>
                {
                    var (index, releaseDate) = kvp.Key;
                    var items = kvp.Value;

                    // For the StageData aggregate, percentage doesn't make sense since different
                    // investments can have different patterns. Leave it at 0 or calculate weighted average if needed.
                    decimal amountPercentage = 0;

                    return new StageData
                    {
                        StageIndex = index,
                        IsDynamic = true, // Mark as dynamic stage
                        Stage = new Stage
                        {
                            ReleaseDate = releaseDate,
                            AmountToRelease = amountPercentage
                        },
                        Items = items.Select(i => i.trx).ToList()
                    };
                })
                .ToList();

        return Result.Success(stageDataList.AsEnumerable());
    }

    private async Task<Result<IList<(Transaction trx, QueryTransaction? trxInfo)>>> GetProjectInvestmentsTransactionsAsync(
        List<ProjectInvestment> projectInvestments)
    {
        var network = networkConfiguration.GetNetwork();

        var tasks = projectInvestments.Select(async investment =>
        {
            var hex = await transactionService.GetTransactionHexByIdAsync(investment.TransactionId);
            var trxInfo = await transactionService.GetTransactionInfoByIdAsync(investment.TransactionId);
            var trx = network.CreateTransaction(hex); //TODO handle null or invalid hex
            return (trx, trxInfo);
        });

        var results = await Task.WhenAll(tasks);
        return Result.Success<IList<(Transaction trx, QueryTransaction? trxInfo)>>(results.ToList());
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
                await transactionService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

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

            return Result.Success(item);
        }

        // item.IsSpent = unconfirmedOutbound.ContainsOutpoint(new Outpoint(item.Trxid, item.Outputindex));
        if (item.IsSpent)
        {
            // the trx is unconfirmed, we wait till
            // it gets confirmed to discover who spent it
            item.SpentType = "pending";
        }

        return Result.Success(item);
    }

    public async Task<Result<InvestmentSpendingLookup>> ScanInvestmentSpends(ProjectInfo project, string transactionId)
    {
        var trxInfo = await transactionService.GetTransactionInfoByIdAsync(transactionId);

        if (trxInfo == null)
            return Result.Failure<InvestmentSpendingLookup>("Transaction not found");

        var trxHex = await transactionService.GetTransactionHexByIdAsync(transactionId);
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
                var spentInfo = await transactionService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

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
                                await transactionService.GetTransactionInfoByIdAsync(response.RecoveryTransactionId);

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
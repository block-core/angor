using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Projects;

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
             Stage = new Angor.Shared.Models.Stage() { ReleaseDate = x.ReleaseDate, AmountToRelease = x.RatioOfTotal },
             StageDate = x.ReleaseDate,
             StageIndex = x.Index,
             Items = [],
             IsDynamic = false // Invest projects have fixed stages
         }).ToList();

        var investmentsResult = await GetProjectInvestmentsTransactionsAsync(projectInvestments);

        if (investmentsResult.IsFailure)
            return Result.Failure<IEnumerable<StageData>>("Failed to retrieve investment transactions: " + investmentsResult.Error);

        ProjectInfo projectInfo = project.ToProjectInfo();

        foreach (var stage in stageDataList)
        {
            var tasks = investmentsResult.Value.Select(tuple =>
                (output: tuple.trxInfo?.Outputs.First(outp => outp.Index == stage.StageIndex + 2)
                ?? null,
                 transaction: tuple.trx, index: stage.StageIndex))
                .Select(x => CheckSpentFund(x.output, x.transaction, projectInfo, x.index));

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

        // Dictionary to group stages by their release date
        var stagesByDate = new Dictionary<DateTime, StageData>();

        foreach (var (trx, trxInfo) in investmentsResult.Value)
        {
            if (trxInfo == null)
                continue;

            var investment = projectInvestments.FirstOrDefault(p => p.TransactionId == trx.GetHash().ToString());
            if (investment == null)
                continue;

            var fundingParams = FundingParameters.CreateFromTransaction(project.ToProjectInfo(), trx);

            if (fundingParams.InvestmentStartDate == null)
                continue;

            var taprootOutputs = trx.Outputs.AsIndexedOutputs()
                .Where(txout => txout.TxOut.ScriptPubKey.IsTaprooOutput())
                .Select(_ => _.TxOut)
                .ToArray();

            var pattern = project.DynamicStagePatterns.FirstOrDefault(p => p.PatternId == fundingParams.PatternId);
            if (pattern == null)
                continue;
            var stageCount = taprootOutputs.Length;

            // Calculate percentage per stage for this investment (equal split)
            var percentagePerStage = 100m / stageCount;

            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                var qouts = trxInfo.Outputs.ElementAt(stageIndex);

                var releaseDate = DynamicStageCalculator.CalculateDynamicStageReleaseDate(
                        fundingParams.InvestmentStartDate.Value,
                        pattern,
                        stageIndex);

                var stageDataResult = await CheckSpentFund(qouts, trx, project.ToProjectInfo(), stageIndex);

                if (stageDataResult.IsFailure)
                    continue;

                var stageDataTrx = stageDataResult.Value;
                stageDataTrx.InvestorPublicKey = investment.InvestorPublicKey;
                stageDataTrx.DynamicReleaseDate = releaseDate;
                stageDataTrx.PatternId = fundingParams.PatternId;
                stageDataTrx.InvestmentStartDate = fundingParams.InvestmentStartDate;
                stageDataTrx.StageIndex = stageIndex;
                stageDataTrx.AmountPercentage = percentagePerStage;

                // Group by release date - if a StageData for this date already exists, add the trx to its items
                var releaseDateKey = releaseDate.Date; // Use date only to group by day
                if (stagesByDate.TryGetValue(releaseDateKey, out var existingStageData))
                {
                    existingStageData.Items.Add(stageDataTrx);
                }
                else
                {
                    stagesByDate[releaseDateKey] = new StageData
                    {
                        StageIndex = stageIndex,
                        StageDate = releaseDate,
                        IsDynamic = true,
                        Items = [stageDataTrx]
                    };
                }
            }
        }

        // Order by date for consistent ordering
        var orderedList = stagesByDate.Values
            .OrderBy(s => s.StageDate)
            .ToList();

        return Result.Success<IEnumerable<StageData>>(orderedList);
    }

    private async Task<Result<IList<(Transaction trx, QueryTransaction? trxInfo)>>> GetProjectInvestmentsTransactionsAsync(List<ProjectInvestment> projectInvestments)
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

        var taprootOutputs = investmentTransaction.Outputs.AsIndexedOutputs()
               .Where(txout => txout.TxOut.ScriptPubKey.IsTaprooOutput())
               .ToArray();

        var txOut = taprootOutputs.ElementAt(stageIndex);

        var item = new StageDataTrx
        {
            Trxid = investmentTransaction.GetHash().ToString(),
            Outputindex = (int)txOut.N,
            OutputAddress = txOut.TxOut.ScriptPubKey.WitHash.GetAddress(network).ToString(),
            Amount = txOut.TxOut.Value.Satoshi
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
                    input.InputTransactionId == transactionId &&
                    input.InputIndex == output.Index);

                if (spentInput != null) //TODO move the script discovery to another class
                {
                    var scriptType = investorTransactionActions.DiscoverUsedScript(project, investmentTransaction, stageIndex, spentInput.WitScript);

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
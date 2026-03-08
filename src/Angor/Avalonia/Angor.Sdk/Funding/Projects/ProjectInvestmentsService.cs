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
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Projects;

public class ProjectInvestmentsService(IProjectService projectService, INetworkConfiguration networkConfiguration,
    IAngorIndexerService angorIndexerService, IInvestorTransactionActions investorTransactionActions,
    ITransactionService transactionService, ILogger<ProjectInvestmentsService> logger) : IProjectInvestmentsService
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
             Stage = new Angor.Shared.Models.Stage() { ReleaseDate = x.ReleaseDate, AmountToRelease = x.RatioOfTotal * 100m },
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
                // Output indices: 0=AngorFee, 1=OP_RETURN, 2+=Taproot stages
                // So taproot stage N is at output index N+2
                var qouts = trxInfo.Outputs.First(o => o.Index == stageIndex + 2);

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
        logger.LogInformation("[ScanInvestmentSpends] Starting scan for project={ProjectId}, txId={TxId}", project.ProjectIdentifier, transactionId);
        
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

        // For Invest projects, use Stages.Count. For Fund/Subscribe (dynamic), determine from taproot outputs.
        // Transaction structure: index 0 = Angor fee, index 1 = OP_RETURN, index 2+ = stage outputs
        var stageCount = project.Stages?.Count > 0
            ? project.Stages.Count
            : investmentTransaction.Outputs.AsIndexedOutputs()
                .Count(o => o.TxOut.ScriptPubKey.IsTaprooOutput());

        logger.LogInformation("[ScanInvestmentSpends] stageCount={StageCount}, projectStagesCount={ProjectStagesCount}", stageCount, project.Stages?.Count ?? 0);

        for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            var output = trxInfo.Outputs.First(f => f.Index == stageIndex + 2);

            logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: SpentInTransaction={SpentTxId}", stageIndex, output.SpentInTransaction ?? "null");

            if (!string.IsNullOrEmpty(output.SpentInTransaction))
            {
                var spentInfo = await transactionService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

                if (spentInfo == null)
                {
                    logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: spentInfo is null, skipping", stageIndex);
                    continue;
                }

                var spentInput = spentInfo.Inputs.FirstOrDefault(input =>
                    input.InputTransactionId == transactionId &&
                    input.InputIndex == output.Index);

                if (spentInput != null) //TODO move the script discovery to another class
                {
                    var scriptType = investorTransactionActions.DiscoverUsedScript(project, investmentTransaction, stageIndex, spentInput.WitScript);

                    logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: DiscoverUsedScript returned {ScriptType}", stageIndex, scriptType.ScriptType);

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
                            logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: EndOfProject, returning", stageIndex);
                            return response;
                        }

                        case ProjectScriptTypeEnum.InvestorWithPenalty:
                        {
                            // InvestorWithPenalty = 2-of-2 multisig spent. Check the output script
                            // at the corresponding index (SIGHASH_SINGLE: input N signs output N)
                            // to distinguish recovery (penalty timelock P2WSH) from unfunded release (P2WPKH).
                            var correspondingOutput = spentInfo.Outputs.FirstOrDefault(o => o.Index == stageIndex);
                            var hasPenaltyTimelock = correspondingOutput != null
                                && !string.IsNullOrEmpty(correspondingOutput.ScriptPubKey)
                                && NBitcoin.Script.FromHex(correspondingOutput.ScriptPubKey)
                                    .IsScriptType(NBitcoin.ScriptType.P2WSH);

                            logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: InvestorWithPenalty, correspondingOutput.Index={OutputIndex}, hasPenaltyTimelock={HasPenalty}", 
                                stageIndex, correspondingOutput?.Index, hasPenaltyTimelock);

                            if (!hasPenaltyTimelock)
                            {
                                // No timelock outputs = unfunded release (direct to investor)
                                response.UnfundedReleaseTransactionId = output.SpentInTransaction;
                                logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: Unfunded release, Set UnfundedReleaseTransactionId={TxId}", stageIndex, output.SpentInTransaction);
                                return response;
                            }

                            response.RecoveryTransactionId = output.SpentInTransaction;
                            var totalsats = trxInfo.Outputs.SkipLast(1).Sum(s => s.Balance);
                            response.AmountInRecovery = totalsats;
                            
                            logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: Penalty recovery, Set RecoveryTransactionId={TxId}", stageIndex, output.SpentInTransaction);

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
                            logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: InvestorNoPenalty, Set UnfundedReleaseTransactionId={TxId}, returning", stageIndex, output.SpentInTransaction);
                            return response;
                        }
                    }
                }
                else
                {
                    logger.LogInformation("[ScanInvestmentSpends] Stage {StageIndex}: spentInput is null (no matching input found)", stageIndex);
                }
            }
        }

        logger.LogInformation("[ScanInvestmentSpends] Completed scan. UnfundedReleaseTxId={UnfundedTxId}, RecoveryTxId={RecoveryTxId}", 
            response.UnfundedReleaseTransactionId ?? "null", response.RecoveryTransactionId ?? "null");

        return response;
    }
}
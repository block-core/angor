using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Investor.Dtos;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Angor.Sdk.Funding.Projects;
using Script = Blockcore.Consensus.ScriptInfo.Script;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class GetRecoveryStatus
{
    public record GetRecoveryStatusRequest(WalletId WalletId, ProjectId ProjectId) : IRequest<Result<GetRecoveryStatusResponse>>;

    public record GetRecoveryStatusResponse(InvestorProjectRecoveryDto RecoveryData);

    public class GetRecoveryStatusHandler(
        IProjectService projectService,
        IPortfolioService investmentService,
        IAngorIndexerService angorIndexerService,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions,
        IProjectInvestmentsService projectInvestmentsService,
        ITransactionService transactionService,
        IInvestmentAppService investmentAppService,
        ILogger<GetRecoveryStatusHandler> logger
    ) : IRequestHandler<GetRecoveryStatusRequest, Result<GetRecoveryStatusResponse>>
    {
        public async Task<Result<GetRecoveryStatusResponse>> Handle(GetRecoveryStatusRequest request,
            CancellationToken cancellationToken)
        {
            var project = await projectService.GetAsync(request.ProjectId);

            if (project.IsFailure)
                return Result.Failure<GetRecoveryStatusResponse>(project.Error);
            
            var investments = await investmentService.GetByWalletId(request.WalletId.Value);

            if (investments.IsFailure)
                return Result.Failure<GetRecoveryStatusResponse>(investments.Error);

            if (investments.Value.ProjectIdentifiers.Count == 0)
                return Result.Failure<GetRecoveryStatusResponse>("No investments found for this wallet");
            
            var investmentRecord = investments.Value.ProjectIdentifiers
                .FirstOrDefault(x => x.ProjectIdentifier == request.ProjectId.Value);
            
            if (investmentRecord == null)
                return Result.Failure<GetRecoveryStatusResponse>("No investments found for this project");

            var investmentDetails = await FindInvestments(project.Value, investmentRecord.InvestorPubKey);
            
            if (investmentDetails.IsFailure)
                return Result.Failure<GetRecoveryStatusResponse>(investmentDetails.Error);
            
            if (!investmentDetails.Value.Item2.Any())
                return Result.Failure<GetRecoveryStatusResponse>("No investment stages found for this project");

            var checkResult = await CheckSpentFund(investmentDetails.Value.Item2.ToList(), investmentDetails.Value.Item1, project.Value);

            if (checkResult.IsFailure)
                return Result.Failure<GetRecoveryStatusResponse>(checkResult.Error);

            var dto = checkResult.Value;

            // Check if the founder has sent release signatures and the investor still has unspent funds
            if (dto.HasUnspentItems)
            {
                var releaseCheck = await investmentAppService.CheckForReleaseSignatures(
                    new CheckForReleaseSignatures.CheckForReleaseSignaturesRequest(request.WalletId, request.ProjectId));

                dto.HasReleaseSignatures = releaseCheck.IsSuccess && releaseCheck.Value.HasReleaseSignatures;
            }

            return Result.Success(new GetRecoveryStatusResponse(dto));
        }



        private async Task<Result<(QueryTransaction,IEnumerable<InvestorStageItemDto>)>> FindInvestments(Project project,string investorPubKey)
        {
            var trxResult = await Result.Try(() => angorIndexerService.GetInvestmentAsync(project.Id.Value, investorPubKey));

            if (trxResult.IsFailure)
                return Result.Failure<(QueryTransaction, IEnumerable<InvestorStageItemDto>)>(trxResult.Error);
            
            var trx = trxResult.Value;
            
            if (trx == null || string.IsNullOrEmpty(trx.TransactionId) || trx.InvestorPublicKey != investorPubKey)
            {
                return Result.Failure<(QueryTransaction,IEnumerable<InvestorStageItemDto>)>("Investment transaction not found");
            }

            var trxInfo = await transactionService.GetTransactionInfoByIdAsync(trx.TransactionId);
            if (trxInfo == null)
            {
                return Result.Failure<(QueryTransaction,IEnumerable<InvestorStageItemDto>)>("Investment transaction info not found");
            }

            // Determine stage count based on project type
            // For all project types, stages correspond to Taproot outputs
            // Transaction structure: index 0 = Angor fee, index 1 = OP_RETURN, index 2+ = stage outputs
            var taprootOutputs = trxInfo.Outputs
                .Where(o => Script.FromHex(o.ScriptPubKey).IsTaprooOutput())
                .OrderBy(o => o.Index)
                .ToList();

            if (!taprootOutputs.Any())
            {
                return Result.Failure<(QueryTransaction, IEnumerable<InvestorStageItemDto>)>("No stage outputs found in investment transaction");
            }

            var stagesData = Result.Try(() => taprootOutputs.Select((output, stageIndex) =>
            {
                return new InvestorStageItemDto
                {
                    StageIndex = stageIndex,
                    Amount = output.Balance, // Store balance in satoshis
                    IsSpent = false,
                    Status = "Unknown",
                    ScriptType = ProjectScriptTypeEnum.Unknown,
                };
            }));

            return stagesData.IsFailure
                ? Result.Failure<(QueryTransaction, IEnumerable<InvestorStageItemDto>)>(stagesData.Error)
                : Result.Success<(QueryTransaction, IEnumerable<InvestorStageItemDto>)>((trxInfo, stagesData.Value));
        }

        private async Task<Result<InvestorProjectRecoveryDto>> CheckSpentFund(IList<InvestorStageItemDto> stageItems, QueryTransaction transactionInfo, Project project)
        {
            //TODO handle unconfirmed outbound transactions

            var projectInfo = project.ToProjectInfo();

            var trxHex = await transactionService.GetTransactionHexByIdAsync(transactionInfo.TransactionId);
            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(trxHex);

            var lookup = await projectInvestmentsService.ScanInvestmentSpends(projectInfo, transactionInfo.TransactionId);

            if (lookup.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(lookup.Error);
            
            var penaltyExpieryDate = Utils.UnixTimeToDateTime(transactionInfo.Timestamp).AddDays(projectInfo.PenaltyDays);

            if (!string.IsNullOrEmpty(lookup.Value.RecoveryTransactionId))
            {
                var recoveryTransaction = await transactionService.GetTransactionInfoByIdAsync(lookup.Value.RecoveryTransactionId);
                if (recoveryTransaction != null)
                    penaltyExpieryDate = Utils.UnixTimeToDateTime(recoveryTransaction.Timestamp).AddDays(projectInfo.PenaltyDays);
            }

            var tasks = stageItems.Select(item => CheckTransactionSpendingAsync(transactionInfo, item, lookup, penaltyExpieryDate,
                investmentTransaction, projectInfo));
            
            await Task.WhenAll(tasks);

            // Calculate total investment amount
            var totalInvestmentAmount = stageItems.Sum(a => a.Amount);
            
            // Check if investment is above penalty threshold
            var isAboveThreshold = investorTransactionActions.IsInvestmentAbovePenaltyThreshold(
                projectInfo, 
                totalInvestmentAmount);
            
            var isEndOfProject = projectInfo.ExpiryDate < DateTime.Now;

            var response = new InvestorProjectRecoveryDto
            {
                HasUnspentItems = stageItems.Any(a => a.IsSpent == false),
                HasItemsInPenalty = (stageItems.Any(a => a.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty) && DateTime.UtcNow > penaltyExpieryDate),
                EndOfProject = isEndOfProject,
                IsAboveThreshold = isAboveThreshold,
                TotalSpendable = stageItems.Where(a => !a.IsSpent).Sum(a => a.Amount),
                TotalInPenalty = stageItems.Where(t => t.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty).Sum(t => t.Amount),
                ExpiryDate = projectInfo.ExpiryDate,
                Name = project.Name,
                PenaltyDays = projectInfo.PenaltyDays,
                ProjectIdentifier = project.Id.Value,
                FounderKey = projectInfo.FounderKey,
                Items = stageItems.ToList()
            };

            return Result.Success(response);
        }

        private async Task<Result> CheckTransactionSpendingAsync(QueryTransaction transactionInfo, InvestorStageItemDto item, Result<InvestmentSpendingLookup> lookup,
            DateTimeOffset penaltyExpieryDate, Transaction? investmentTransaction, ProjectInfo projectInfo)
        {
            //TODO handle unconfirmed outbound transactions
            
            var output = transactionInfo.Outputs.ElementAt(item.StageIndex + 2);

            logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: SpentInTransaction={SpentTxId}, UnfundedReleaseTxId={UnfundedTxId}, RecoveryTxId={RecoveryTxId}", 
                item.StageIndex, output.SpentInTransaction ?? "null", lookup.Value.UnfundedReleaseTransactionId ?? "null", lookup.Value.RecoveryTransactionId ?? "null");

            if (!string.IsNullOrEmpty(output.SpentInTransaction))
            {
                item.IsSpent = true;

                if (output.SpentInTransaction == lookup.Value.UnfundedReleaseTransactionId)
                {
                    item.Status = "Project Unfunded, Spent back to investor";
                    item.ScriptType = ProjectScriptTypeEnum.InvestorNoPenalty;
                    logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: Matched UnfundedReleaseTransactionId, status='Project Unfunded, Spent back to investor'", item.StageIndex);
                }
                else
                {
                    if (output.SpentInTransaction == lookup.Value.RecoveryTransactionId)
                    {
                        logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: Matched RecoveryTransactionId", item.StageIndex);
                        if (!string.IsNullOrEmpty(lookup.Value.RecoveryReleaseTransactionId))
                        {
                            item.Status = "Recovered after penalty";
                            item.ScriptType = ProjectScriptTypeEnum.Unknown;
                        }
                        else
                        {
                            item.ScriptType = ProjectScriptTypeEnum.InvestorWithPenalty;
                            var days = (penaltyExpieryDate - DateTime.Now).TotalDays;
                            item.Status = days > 0
                                ? $"Penalty, released in {days.ToString("0.0")} days"
                                : "Penalty can be released";
                        }
                        logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: status='{Status}'", item.StageIndex, item.Status);
                    }
                    else
                    {
                        logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: No lookup match, falling through to DiscoverUsedScript", item.StageIndex);
                        // try to resolve the destination
                        var spentInTransaction =
                            await transactionService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

                        var input = spentInTransaction?.Inputs.FirstOrDefault(input =>
                            input.InputTransactionId == transactionInfo.TransactionId &&
                            input.InputIndex == item.StageIndex + 2);

                        logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: spentInTransaction found={Found}, input found={InputFound}", 
                            item.StageIndex, spentInTransaction != null, input != null);

                        if (input != null && investmentTransaction != null)
                        {
                            var script = investorTransactionActions.DiscoverUsedScript(projectInfo,
                                investmentTransaction, item.StageIndex, input.WitScript);

                            item.ScriptType = script.ScriptType;
                            logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: DiscoverUsedScript returned {ScriptType}", item.StageIndex, script.ScriptType);

                            switch (script.ScriptType)
                            {
                                case ProjectScriptTypeEnum.Founder:
                                {
                                    item.Status = "Spent by founder";
                                    break;
                                }
                                case ProjectScriptTypeEnum.InvestorWithPenalty:
                                {
                                    // Both recovery and unfunded release use the same Recover script path.
                                    // Check spending transaction outputs to distinguish:
                                    // - P2WPKH outputs = unfunded release (direct to investor)
                                    // - P2WSH outputs = recovery with penalty
                                    
                                    // Log spending tx output types
                                    if (spentInTransaction != null)
                                    {
                                        foreach (var spentOutput in spentInTransaction.Outputs)
                                        {
                                            logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: SpentTx output idx={OutputIdx}, OutputType={OutputType}, Address={Address}", 
                                                item.StageIndex, spentOutput.Index, spentOutput.OutputType ?? "null", spentOutput.Address ?? "null");
                                        }
                                    }
                                    
                                    if (spentInTransaction != null && spentInTransaction.Outputs
                                            .Any(o => o.OutputType == "witness_v0_keyhash" || o.OutputType == "v0_p2wpkh"))
                                    {
                                        item.Status = "Project Unfunded, Spent back to investor";
                                        item.ScriptType = ProjectScriptTypeEnum.InvestorNoPenalty;
                                        logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: Detected unfunded release via output type, status='Project Unfunded, Spent back to investor'", item.StageIndex);
                                    }
                                    else
                                    {
                                        // Check if the recovery transaction's penalty outputs have been spent
                                        var recoveryTrxInfo = spentInTransaction;
                                        if (recoveryTrxInfo != null && recoveryTrxInfo.Outputs.SkipLast(1)
                                                .Any(o => !string.IsNullOrEmpty(o.SpentInTransaction)))
                                        {
                                            item.Status = "Recovered after penalty";
                                            item.ScriptType = ProjectScriptTypeEnum.Unknown;
                                        }
                                        else
                                        {
                                            var days = (penaltyExpieryDate - DateTime.Now).TotalDays;
                                            item.Status = days > 0
                                                ? $"Penalty, released in {days.ToString("0.0")} days"
                                                : "Penalty can be released";
                                        }
                                        logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: Penalty path, status='{Status}'", item.StageIndex, item.Status);
                                    }
                                    break;
                                }
                                case ProjectScriptTypeEnum.EndOfProject:
                                case ProjectScriptTypeEnum.InvestorNoPenalty:
                                {
                                    item.Status = $"Spent by investor";
                                    break;
                                }
                                case ProjectScriptTypeEnum.Unknown:
                                    logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: ScriptType is Unknown", item.StageIndex);
                                    break;
                                default:
                                    return Result.Failure(
                                        $"Unknown script type {item.ScriptType} on stage {item.StageIndex}");
                            }
                            
                            logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: Final status='{Status}', scriptType={ScriptType}", item.StageIndex, item.Status, item.ScriptType);
                        }
                    }
                }
            }
            else
            {
                item.Status = string.Empty;
                logger.LogInformation("[CheckTxSpending] Stage {StageIndex}: Not spent, status=''", item.StageIndex);
            }
            
            return Result.Success();
        }
    }
}

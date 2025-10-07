using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class GetInvestorProjectRecovery
{
    public record GetInvestorProjectRecoveryRequest(Guid WalletId, ProjectId ProjectId) : IRequest<Result<InvestorProjectRecoveryDto>>;

    public class Handler(
        IProjectRepository projectRepository,
        IPortfolioRepository investmentRepository,
        IIndexerService indexerService,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions,
        IProjectInvestmentsRepository projectInvestmentsRepository
    ) : IRequestHandler<GetInvestorProjectRecoveryRequest, Result<InvestorProjectRecoveryDto>>
    {
        public async Task<Result<InvestorProjectRecoveryDto>> Handle(GetInvestorProjectRecoveryRequest request,
            CancellationToken cancellationToken)
        {
            var project = await projectRepository.Get(request.ProjectId);

            if (project.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(project.Error);
            
            var investments = await investmentRepository.GetByWalletId(request.WalletId);

            if (investments.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(investments.Error);

            if (investments.Value.ProjectIdentifiers.Count == 0)
                return Result.Failure<InvestorProjectRecoveryDto>("No investments found for this wallet");
            
            var investmentRecord = investments.Value.ProjectIdentifiers
                .FirstOrDefault(x => x.ProjectIdentifier == request.ProjectId.Value);
            
            if (investmentRecord == null)
                return Result.Failure<InvestorProjectRecoveryDto>("No investments found for this project");

            var investmentDetails = await FindInvestments(project.Value, investmentRecord.InvestorPubKey);
            
            if (investmentDetails.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(investmentDetails.Error);
            
            if (!investmentDetails.Value.Item2.Any())
                return Result.Failure<InvestorProjectRecoveryDto>("No investment stages found for this project");

            return await CheckSpentFund(investmentDetails.Value.Item2.ToList(), investmentDetails.Value.Item1, project.Value);
        }



        private async Task<Result<(QueryTransaction,IEnumerable<InvestorStageItemDto>)>> FindInvestments(Project project,string investorPubKey)
        {
            var trxResult = await Result.Try(() => indexerService.GetInvestmentAsync(project.Id.Value, investorPubKey));

            if (trxResult.IsFailure)
                return Result.Failure<(QueryTransaction, IEnumerable<InvestorStageItemDto>)>(trxResult.Error);
            
            var trx = trxResult.Value;
            
            if (trx == null || string.IsNullOrEmpty(trx.TransactionId) || trx.InvestorPublicKey != investorPubKey)
            {
                return Result.Failure<(QueryTransaction,IEnumerable<InvestorStageItemDto>)>("Investment transaction not found");
            }

            var trxInfo = await indexerService.GetTransactionInfoByIdAsync(trx.TransactionId);
            if (trxInfo == null)
            {
                return Result.Failure<(QueryTransaction,IEnumerable<InvestorStageItemDto>)>("Investment transaction info not found");
            }

            var stagesData = Result.Try(() => project.Stages.Select((_, stageIndex) =>
            {
                var output = trxInfo.Outputs.First(f => f.Index == stageIndex + 2);
                
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

            var trxHex = await indexerService.GetTransactionHexByIdAsync(transactionInfo.TransactionId);
            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(trxHex);

            var lookup = await projectInvestmentsRepository.ScanInvestmentSpends(projectInfo, transactionInfo.TransactionId);

            if (lookup.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(lookup.Error);
            
            var penaltyExpieryDate =
                Utils.UnixTimeToDateTime(transactionInfo.Timestamp).AddDays(projectInfo.PenaltyDays);

            if (!string.IsNullOrEmpty(lookup.Value.RecoveryTransactionId))
            {
                var recoveryTansaction =
                    await indexerService.GetTransactionInfoByIdAsync(lookup.Value.RecoveryTransactionId);
                if (recoveryTansaction != null)
                    penaltyExpieryDate = Utils.UnixTimeToDateTime(recoveryTansaction.Timestamp)
                        .AddDays(projectInfo.PenaltyDays);
            }


            var tasks = stageItems.Select(item => CheckTransactionSpendingAsync(transactionInfo, item, lookup, penaltyExpieryDate,
                investmentTransaction, projectInfo));
            
            await Task.WhenAll(tasks);

            var response = new InvestorProjectRecoveryDto
            {
                CanRecover = stageItems.Any(a => a.IsSpent == false),
                CanRelease = (stageItems.Any(a => a.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty) &&
                              DateTime.UtcNow > penaltyExpieryDate),
                TotalSpendable = stageItems.Where(a => !a.IsSpent).Sum(a => a.Amount),
                TotalInPenalty = stageItems.Where(t => t.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty)
                    .Sum(t => t.Amount),
                EndOfProject = projectInfo.ExpiryDate < DateTime.Now,
                ExpiryDate = projectInfo.ExpiryDate,
                Name = project.Name,
                PenaltyDays = projectInfo.PenaltyDays,
                ProjectIdentifier = project.Id.Value,
                Items = stageItems.ToList()
            };

            return Result.Success(response);
        }

        private async Task<Result> CheckTransactionSpendingAsync(QueryTransaction transactionInfo, InvestorStageItemDto item, Result<InvestmentSpendingLookup> lookup,
            DateTimeOffset penaltyExpieryDate, Transaction? investmentTransaction, ProjectInfo projectInfo)
        {
            //TODO handle unconfirmed outbound transactions
            
            var output = transactionInfo.Outputs.ElementAt(item.StageIndex + 2);

            if (!string.IsNullOrEmpty(output.SpentInTransaction))
            {
                item.IsSpent = true;

                if (output.SpentInTransaction == lookup.Value.UnfundedReleaseTransactionId)
                {
                    item.Status = "Project Unfunded, Spent back to investor";
                    item.ScriptType = ProjectScriptTypeEnum.InvestorNoPenalty;
                }
                else
                {
                    if (output.SpentInTransaction == lookup.Value.RecoveryTransactionId)
                    {
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
                    }
                    else
                    {
                        // try to resolve the destination
                        var spentInTransaction =
                            await indexerService.GetTransactionInfoByIdAsync(output.SpentInTransaction);

                        var input = spentInTransaction?.Inputs.FirstOrDefault(input =>
                            input.InputTransactionId == transactionInfo.TransactionId &&
                            input.InputIndex == item.StageIndex + 2);

                        if (input != null && investmentTransaction != null)
                        {
                            var script = investorTransactionActions.DiscoverUsedScript(projectInfo,
                                investmentTransaction, item.StageIndex, input.WitScript);

                            item.ScriptType = script.ScriptType;

                            switch (script.ScriptType)
                            {
                                case ProjectScriptTypeEnum.Founder:
                                {
                                    item.Status = "Spent by founder";
                                    break;
                                }
                                case ProjectScriptTypeEnum.InvestorWithPenalty:
                                {
                                    var days = (penaltyExpieryDate - DateTime.Now).TotalDays;
                                    item.Status = days > 0
                                        ? $"Penalty, released in {days.ToString("0.0")} days"
                                        : "Penalty can be released";
                                    break;
                                }
                                case ProjectScriptTypeEnum.EndOfProject:
                                case ProjectScriptTypeEnum.InvestorNoPenalty:
                                {
                                    item.Status = $"Spent by investor";
                                    break;
                                }
                                case ProjectScriptTypeEnum.Unknown:
                                    break;
                                default:
                                    return Result.Failure(
                                        $"Unknown script type {item.ScriptType} on stage {item.StageIndex}");
                            }
                        }
                    }
                }
            }
            else
            {
                item.Status = string.Empty;
            }
            
            return Result.Success();
        }
    }
}

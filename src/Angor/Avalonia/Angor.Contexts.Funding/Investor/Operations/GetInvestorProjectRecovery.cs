using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class GetInvestorProjectRecovery
{
    public record GetInvestorProjectRecoveryRequest(Guid WalletId, ProjectId ProjectId) : IRequest<Result<InvestorProjectRecoveryDto>>;

    public class Handler(
        IInvestmentRepository investmentRepository,
        IProjectRepository projectRepository,
        IIndexerService indexerService,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions
    ) : IRequestHandler<GetInvestorProjectRecoveryRequest, Result<InvestorProjectRecoveryDto>>
    {
        public async Task<Result<InvestorProjectRecoveryDto>> Handle(GetInvestorProjectRecoveryRequest request, CancellationToken cancellationToken)
        {
            var portfolio = await investmentRepository.GetByWalletId(request.WalletId);
            if (portfolio.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(portfolio.Error);

            var projectResult = await projectRepository.Get(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure<InvestorProjectRecoveryDto>(projectResult.Error);

            var project = projectResult.Value;

            var record = portfolio.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (record is null)
                return Result.Failure<InvestorProjectRecoveryDto>("No investment record found for this project and wallet");

            // Load investment transaction info and hex
            var trxInfo = await indexerService.GetTransactionInfoByIdAsync(record.InvestmentTransactionHash);
            var trxHex = await indexerService.GetTransactionHexByIdAsync(record.InvestmentTransactionHash);
            if (trxInfo is null || string.IsNullOrEmpty(trxHex))
                return Result.Failure<InvestorProjectRecoveryDto>("Investment transaction not found on indexer");

            var transaction = networkConfiguration.GetNetwork().CreateTransaction(trxHex);

            var dto = new InvestorProjectRecoveryDto
            {
                ProjectIdentifier = request.ProjectId.Value,
                Name = project.Name,
                ExpiryDate = project.ExpiryDate,
                PenaltyDays = project.PenaltyDuration.Days,
            };

            long totalSpendable = 0;
            long totalInPenalty = 0;

            DateTimeOffset? penaltyExpiry = null;

            for (int stageIndex = 0; stageIndex < project.Stages.Count(); stageIndex++)
            {
                var outIndex = stageIndex + 2; // skip angor+change
                var output = trxInfo.Outputs.FirstOrDefault(o => o.Index == outIndex);
                long amount = output?.Balance ?? 0;

                var item = new InvestorStageItemDto { StageIndex = stageIndex, Amount = amount };

                if (output == null || string.IsNullOrEmpty(output.SpentInTransaction))
                {
                    // Not spent
                    item.IsSpent = false;
                    item.Status = "Not Spent";
                    totalSpendable += amount;
                }
                else
                {
                    item.IsSpent = true;
                    // Discover script type
                    var spentInfo = await indexerService.GetTransactionInfoByIdAsync(output.SpentInTransaction);
                    var spentInput = spentInfo?.Inputs.FirstOrDefault(i => i.InputTransactionId == record.InvestmentTransactionHash && i.InputIndex == outIndex);

                    if (spentInput != null)
                    {
                        var scriptType = investorTransactionActions.DiscoverUsedScript(project.ToProjectInfo(), transaction, stageIndex, spentInput.WitScript);
                        item.ScriptType = scriptType.ScriptType;

                        switch (scriptType.ScriptType)
                        {
                            case ProjectScriptTypeEnum.Founder:
                                item.Status = "Spent by founder";
                                break;
                            case ProjectScriptTypeEnum.InvestorNoPenalty:
                                item.Status = "Spent by investor";
                                break;
                            case ProjectScriptTypeEnum.EndOfProject:
                                item.Status = "End of project";
                                break;
                            case ProjectScriptTypeEnum.InvestorWithPenalty:
                                // Determine remaining days
                                var recoveryTx = await indexerService.GetTransactionInfoByIdAsync(output.SpentInTransaction);
                                if (recoveryTx != null)
                                {
                                    var expiry = Utils.UnixTimeToDateTime(recoveryTx.Timestamp).AddDays(project.PenaltyDuration.Days);
                                    var days = (expiry - DateTime.UtcNow).TotalDays;
                                    if (days > 0)
                                    {
                                        item.Status = $"Penalty, released in {days:0.0} days";
                                    }
                                    else
                                    {
                                        item.Status = "Penalty can be released";
                                    }
                                    penaltyExpiry ??= expiry;
                                }
                                totalInPenalty += amount;
                                break;
                            default:
                                item.Status = "Unknown";
                                break;
                        }
                    }
                    else
                    {
                        item.Status = "Spent";
                    }
                }

                dto.Items.Add(item);
            }

            dto.TotalSpendable = totalSpendable;
            dto.TotalInPenalty = totalInPenalty;
            dto.CanRecover = dto.Items.Any(i => !i.IsSpent);
            dto.EndOfProject = project.ExpiryDate < DateTime.UtcNow;
            dto.CanRelease = totalInPenalty > 0 && penaltyExpiry.HasValue && DateTime.UtcNow >= penaltyExpiry.Value;

            return Result.Success(dto);
        }
    }
}

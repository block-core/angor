using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Projects.Operations;

public static class ProjectStatistics
{
    public record ProjectStatsRequest(ProjectId ProjectId) : IRequest<Result<ProjectStatisticsDto>>;

    public class ProjectStatsHandler(IProjectInvestmentsService projectInvestmentsService) : IRequestHandler<ProjectStatsRequest, Result<ProjectStatisticsDto>>
    {
        public async Task<Result<ProjectStatisticsDto>> Handle(ProjectStatsRequest request, CancellationToken cancellationToken)
        {
            var stagesInformation = await projectInvestmentsService.ScanFullInvestments(request.ProjectId.Value);

            if (stagesInformation.IsFailure)
            {
                return Result.Failure<ProjectStatisticsDto>(stagesInformation.Error);
            }

            return Result.Try(() => CalculateTotalValues(stagesInformation.Value.ToList()));
        }

        private static ProjectStatisticsDto CalculateTotalValues(List<StageData> stagesInformation)
        {
            if (!stagesInformation.Any())
            {
                return new ProjectStatisticsDto
                {
                    TotalStages = 0,
                    NextStage = null,
                    DynamicStages = null
                };
            }

            var isDynamicProject = stagesInformation.Any(s => s.IsDynamic);

            var nextStage = stagesInformation
                    .Where(stage => stage.Stage.ReleaseDate > DateTime.UtcNow)
                    .OrderBy(stage => stage.Stage.ReleaseDate)
                    .FirstOrDefault();

            var currentStage = stagesInformation
                .OrderBy(x => x.Stage.ReleaseDate)
                .LastOrDefault(stage => stage.Stage.ReleaseDate <= DateTime.UtcNow);

            currentStage ??= stagesInformation.OrderBy(x => x.Stage.ReleaseDate).FirstOrDefault();

            var dto = new ProjectStatisticsDto
            {
                TotalStages = CalculateTotalStages(stagesInformation, isDynamicProject),
            };

            if (currentStage != null)
            {
                dto.NextStage = new NextStageDto
                {
                    PercentageToRelease = CalculateStagePercentage(currentStage, isDynamicProject),
                    ReleaseDate = currentStage.Stage.ReleaseDate,
                    DaysUntilRelease = nextStage != null ? (nextStage.Stage.ReleaseDate - DateTime.UtcNow).Days : 0,
                    StageIndex = nextStage != null ? stagesInformation.IndexOf(nextStage) : stagesInformation.Count - 1,
                };
            }

            foreach (var stage in stagesInformation)
            {
                var totalStageTransactions = stage.Items.Count;
                var investedAmount = stage.Items.Sum(c => c.Amount);
                var availableInvestedAmount = stage.Items.Where(c => !c.IsSpent).Sum(c => c.Amount);
                var spentStageAmount = stage.Items.Where(c => c.IsSpent).Sum(c => c.Amount);
                var spentStageTransactions = stage.Items.Count(c => c.IsSpent);
                var daysUntilRelease = stage.Stage.ReleaseDate.Date < DateTime.UtcNow.Date ? 0 : (stage.Stage.ReleaseDate.Date - DateTime.UtcNow.Date).Days;

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

            if (isDynamicProject)
            {
                dto.TotalInvestors = CalculateUniqueInvestors(stagesInformation);
                dto.DynamicStages = MapDynamicStages(stagesInformation);
            }

            return dto;
        }

        private static List<DynamicStageDto> MapDynamicStages(List<StageData> stages)
        {
            return stages
                    .OrderBy(s => s.Stage.ReleaseDate)
                    .ThenBy(s => s.StageIndex)
                    .Select(stage => new DynamicStageDto
                    {
                        StageIndex = stage.StageIndex,
                        ReleaseDate = stage.Stage.ReleaseDate,
                        TotalAmount = stage.TotalAmount,
                        TransactionCount = stage.Items.Count,
                        UnspentTransactionCount = stage.Items.Count(i => !i.IsSpent),
                        UnspentAmount = stage.Items.Where(i => !i.IsSpent).Sum(i => i.Amount)
                    })
                    .ToList();
        }

        private static int CalculateTotalStages(List<StageData> stages, bool isDynamic)
        {
            if (!isDynamic)
                return stages.Count != 0 ? stages.Count : 1;

            return stages
                    .SelectMany(s => s.Items)
                    .GroupBy(i => i.Trxid)
                    .Select(g => g.Max(i => i.StageIndex) + 1)
                    .DefaultIfEmpty(0)
                    .Max();
        }

        private static decimal CalculateStagePercentage(StageData stage, bool isDynamic)
        {
            if (!isDynamic)
                return stage.Stage.AmountToRelease;

            if (!stage.Items.Any())
                return 0;

            var totalAmount = stage.Items.Sum(i => i.Amount);
            if (totalAmount == 0)
                return 0;

            var weightedPercentage = stage.Items
                    .Where(i => i.AmountPercentage.HasValue)
                    .Sum(i => (i.Amount / (decimal)totalAmount) * i.AmountPercentage.Value);

            return weightedPercentage;
        }

        private static int CalculateUniqueInvestors(List<StageData> stages)
        {
            return stages
                    .SelectMany(s => s.Items)
                    .Where(i => !string.IsNullOrEmpty(i.InvestorPublicKey))
                    .Select(i => i.InvestorPublicKey)
                    .Distinct()
                    .Count();
        }
    }
}
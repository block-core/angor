using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class ProjectStatistics
{
    public record ProjectStatsRequest(ProjectId ProjectId) : IRequest<Result<ProjectStatisticsDto>>;

    public class ProjectStatsHandler(
        IProjectInvestmentsService projectInvestmentsService,
        IProjectService projectService,
        IMemoryCache memoryCache) : IRequestHandler<ProjectStatsRequest, Result<ProjectStatisticsDto>>
    {
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(1);

        private static string GetCacheKey(ProjectId projectId) => $"ProjectStats_{projectId.Value}";

        public async Task<Result<ProjectStatisticsDto>> Handle(ProjectStatsRequest request, CancellationToken cancellationToken)
        {
            var cacheKey = GetCacheKey(request.ProjectId);

            if (memoryCache.TryGetValue(cacheKey, out ProjectStatisticsDto? cachedResult) && cachedResult != null)
            {
                return Result.Success(cachedResult);
            }

            var stagesInformation = await projectInvestmentsService.ScanFullInvestments(request.ProjectId.Value);

            if (stagesInformation.IsFailure)
            {
                return Result.Failure<ProjectStatisticsDto>(stagesInformation.Error);
            }

            var project = await projectService.GetAsync(request.ProjectId);
            var result = Result.Try(() => CalculateTotalValues(stagesInformation.Value.ToList(), project.Value.ToProjectInfo()));

            if (result.IsSuccess)
            {
                var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(CacheExpiration);
                memoryCache.Set(cacheKey, result.Value, cacheOptions);
            }

            return result;
        }

        private static ProjectStatisticsDto CalculateTotalValues(List<StageData> stagesInformation, ProjectInfo projectInfo)
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
                .Where(stage => stage.StageDate > DateTime.UtcNow)
                .OrderBy(stage => stage.StageDate)
                .FirstOrDefault();

            var currentStage = stagesInformation
                .OrderBy(x => x.StageDate)
                .LastOrDefault(stage => stage.StageDate <= DateTime.UtcNow);

            currentStage ??= stagesInformation.OrderBy(x => x.StageDate).FirstOrDefault();

            var dto = new ProjectStatisticsDto
            {
                TotalStages = stagesInformation.Count
            };

            if (currentStage != null)
            {
                dto.NextStage = new NextStageDto
                {
                    PercentageToRelease = CalculateStagePercentage(currentStage, isDynamicProject, projectInfo),
                    ReleaseDate = currentStage.StageDate,
                    DaysUntilRelease = nextStage != null ? (nextStage.StageDate - DateTime.UtcNow).Days : 0,
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
                var daysUntilRelease = stage.StageDate.Date < DateTime.UtcNow.Date ? 0 : (stage.StageDate.Date - DateTime.UtcNow.Date).Days;

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
                    .OrderBy(s => s.StageDate)
                    .ThenBy(s => s.StageIndex)
                    .Select(stage => new DynamicStageDto
                    {
                        StageIndex = stage.StageIndex,
                        ReleaseDate = stage.StageDate,
                        TotalAmount = stage.TotalAmount,
                        TransactionCount = stage.Items.Count,
                        UnspentTransactionCount = stage.Items.Count(i => !i.IsSpent),
                        UnspentAmount = stage.Items.Where(i => !i.IsSpent).Sum(i => i.Amount)
                    })
                    .ToList();
        }

        private static decimal CalculateStagePercentage(StageData stage, bool isDynamic, ProjectInfo projectInfo)
        {
            if (!isDynamic)
            {
                // For fixed stages, calculate percentage from items if available
                if (!stage.Items.Any())
                    return 0;


                return projectInfo.Stages.ElementAt(stage.StageIndex).AmountToRelease;
            }

            return 0;
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
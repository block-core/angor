using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Projects.Operations;

public static class ProjectStatistics
{
    public record ProjectStatsRequest(ProjectId ProjectId) : IRequest<Result<ProjectStatisticsDto>>;
    
    public class ProjectStatsHandler(IProjectInvestmentsService projectInvestmentsRepository) : IRequestHandler<ProjectStatsRequest, Result<ProjectStatisticsDto>>
    {
        public async Task<Result<ProjectStatisticsDto>> Handle(ProjectStatsRequest request, CancellationToken cancellationToken)
        {
            var stagesInformation = await projectInvestmentsRepository.ScanFullInvestments(request.ProjectId.Value);
            
            if (stagesInformation.IsFailure)
            {
                return Result.Failure<ProjectStatisticsDto>(stagesInformation.Error);
            }
            
            return Result.Try(() => CalculateTotalValues(stagesInformation.Value.ToList()));
        }
        
        private static ProjectStatisticsDto CalculateTotalValues(List<StageData> stagesInformation)
        {
            var nextStage = stagesInformation.Where(stage => stage.Stage.ReleaseDate > DateTime.UtcNow)
                .OrderBy(stage => stage.Stage.ReleaseDate)
                .FirstOrDefault();

            var currentStage = stagesInformation.OrderBy(x => x.StageIndex).LastOrDefault(stage => stage.Stage.ReleaseDate <= DateTime.UtcNow);
            
            currentStage ??= stagesInformation.OrderBy(x => x.StageIndex).FirstOrDefault();
            
            var dto = new ProjectStatisticsDto
            {
                NextStage = new NextStageDto()
                {
                    PercentageToRelease = currentStage.Stage.AmountToRelease, 
                    ReleaseDate = currentStage.Stage.ReleaseDate,
                    DaysUntilRelease = nextStage != null ? (nextStage.Stage.ReleaseDate - DateTime.UtcNow).Days : 0,
                    StageIndex = nextStage != null ? stagesInformation.IndexOf(nextStage) : stagesInformation.Count - 1,
                },
                TotalStages = stagesInformation.Count != 0 ? stagesInformation.Count : 1,
            };

            foreach (var stage in stagesInformation)
            {
                var totalStageTransactions = stage.Items.Count();
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
            return dto;
        }
    }
}
using Angor.Sdk.Funding.Projects.Dtos;

namespace AngorApp.Model.ProjectsV2.InvestmentProject;

public class Stage(int id, DateTimeOffset releaseDate, decimal ratio, IAmountUI total, StageStatus status) : IStage
{
    public int Id { get; } = id;
    public DateTimeOffset ReleaseDate { get; } = releaseDate;
    public decimal Ratio { get; } = ratio;
    public IAmountUI Total { get; } = total;
    public StageStatus Status { get; } = status;

    public static IReadOnlyCollection<IStage> MapFrom(List<StageDto> stages, long targetAmount)
    {
        return stages.Select(s => (IStage)new Stage(
            id: s.Index,
            releaseDate: s.ReleaseDate,
            ratio: s.RatioOfTotal,
            total: new AmountUI(targetAmount),
            status: s.ReleaseDate <= DateTime.UtcNow ? StageStatus.Released : StageStatus.Pending
        )).ToList();
    }
}

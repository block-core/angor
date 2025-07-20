namespace AngorApp.Sections.Founder.ManageFunds;

public interface IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
}
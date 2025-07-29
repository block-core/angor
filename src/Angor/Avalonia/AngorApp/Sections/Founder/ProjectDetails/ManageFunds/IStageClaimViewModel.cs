namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public interface IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
}
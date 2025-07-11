namespace AngorApp.Sections.Founder.ManageFunds.Models;

public interface IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; }
}
namespace AngorApp.Sections.Founder.ManageFunds;

public class StageClaimViewModelDesign : ReactiveObject, IStageClaimViewModel
{
    public IEnumerable<IClaimableStage> ClaimableStages { get; set; }
    public DateTime EstimatedCompletion { get; set; }
}
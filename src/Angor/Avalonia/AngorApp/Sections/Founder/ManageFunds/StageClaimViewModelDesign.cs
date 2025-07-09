using Avalonia.Controls.Selection;

namespace AngorApp.Sections.Founder.ManageFunds;

public class StageClaimViewModelDesign : IStageClaimViewModel
{
    public SelectionModel<IClaimableStage> SelectionModel { get; }
    public IEnumerable<IClaimableStage> ClaimableStages { get; set; }
    public DateTime EstimatedCompletion { get; set; }
}
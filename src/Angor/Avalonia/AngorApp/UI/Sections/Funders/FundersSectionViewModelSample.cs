namespace AngorApp.UI.Sections.Funders;

public class FundersSectionViewModelSample : IFundersSectionViewModel
{
    public IEnhancedCommand ApproveAll { get; } = ReactiveCommand.Create(() => { }).Enhance();
}

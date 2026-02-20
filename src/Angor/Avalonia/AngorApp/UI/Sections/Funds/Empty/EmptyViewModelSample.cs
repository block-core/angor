namespace AngorApp.UI.Sections.Funds.Empty;

public class EmptyViewModelSample : IEmptyViewModel
{
    public IEnhancedCommand AddWallet { get; } = ReactiveCommand.Create(() => { }).Enhance();
}

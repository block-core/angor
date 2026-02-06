namespace AngorApp.UI.Sections.Funds.Empty;

public class EmptyViewModelSample : IEmptyViewModel
{
    public IEnhancedCommand<Unit> AddWallet { get; } = ReactiveCommand.Create(() => { }).Enhance();
}

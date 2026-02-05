namespace AngorApp.UI.Sections.Funds.Empty
{
    public interface IEmptyViewModel
    {
        IEnhancedCommand<Unit> AddWallet { get; }
    }
}
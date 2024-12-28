namespace AngorApp.Sections.Portfolio;

public class PortfolioSectionViewModel : ReactiveObject, IPortfolioSectionViewModel
{
    public PortfolioSectionViewModel()
    {
        Items =
        [
            new PortfolioItem("Ariton", "0"),
            new PortfolioItem("Total invested", "0 TBTC"),
            new PortfolioItem("Wallet", "0 TBTC"),
            new PortfolioItem("In Recovery", "0 TBTC"),
        ];
    }

    public IReadOnlyCollection<PortfolioItem> Items { get; }
}
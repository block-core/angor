namespace AngorApp.Sections.Portfolio;

public interface IPortfolioSectionViewModel
{
    IReadOnlyCollection<PortfolioItem> Items { get; }
}
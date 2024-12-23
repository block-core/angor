namespace AngorApp.Sections.Portfolio;

public interface IPortfolioViewModel
{
    IReadOnlyCollection<PortfolioItem> Items { get; }
}
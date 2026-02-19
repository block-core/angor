using Zafiro.CSharpFunctionalExtensions;
using AngorApp.UI.Sections.Funders.Grouping;

namespace AngorApp.UI.Sections.Funders;

public interface IFundersSectionViewModel
{
    public IEnumerable<IFunderGroup> Groups { get; }
    public IEnhancedCommand Load { get; }
    public IObservable<bool> IsEmpty { get; }
}
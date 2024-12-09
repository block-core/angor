using Zafiro.Avalonia.Controls.Navigation;
using Zafiro.UI;

namespace AngorApp.Sections.Browse;

public interface IBrowseViewModel
{
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}
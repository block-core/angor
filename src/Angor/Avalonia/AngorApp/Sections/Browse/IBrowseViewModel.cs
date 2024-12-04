using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace AngorApp;

public interface IBrowseViewModel
{
    public IReadOnlyCollection<Project> Projects { get; set; }
    ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}
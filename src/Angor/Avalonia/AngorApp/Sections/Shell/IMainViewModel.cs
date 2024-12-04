using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace AngorApp.Sections.Shell;

public interface IMainViewModel
{
    ReactiveCommand<Unit, Unit> OpenHub { get; }
    IEnumerable<SectionBase> Sections { get; }
    Section SelectedSection { get; set; }
}
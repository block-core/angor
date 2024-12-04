using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Shell;

public partial class MainViewModel : ReactiveObject
{
    [Reactive] private Section selectedSection;

    public MainViewModel(IEnumerable<SectionBase> sections, UIServices uiServices)
    {
        Sections = sections;
        SelectedSection = Sections.OfType<Section>().Skip(1).First();
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.Launch(new Uri("https://www.angor.io")));
    }

    public ReactiveCommand<Unit,Unit> OpenHub { get; set; }

    public IEnumerable<SectionBase> Sections { get; }
}

public class Separator : SectionBase;

public class Section(string name, object viewModel, object? icon = null) : SectionBase
{
    public string Name { get; } = name;
    public object ViewModel { get; } = viewModel;
    public object? Icon { get; } = icon;
}
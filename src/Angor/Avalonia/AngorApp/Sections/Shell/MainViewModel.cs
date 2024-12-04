using System.Collections.Generic;
using System.Linq;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Home;
using AngorApp.Sections.Wallet;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Shell;

public partial class MainViewModel : ReactiveObject
{
    [Reactive] private Section selectedSection;

    public MainViewModel()
    {
        Sections =
        [
            new Section("Home", new HomeViewModel(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", new WalletViewModel(), "fa-wallet"),
            new Section("Browse", new BrowseViewModel(), "fa-magnifying-glass"),
            new Section("Portfolio", new WalletViewModel(), "fa-hand-holding-dollar"),
            new Section("Founder", new WalletViewModel(), "fa-money-bills"),
            new Separator(),
            new Section("Settings", new WalletViewModel(), "fa-gear"),
            new Section("Angor Hub", new WalletViewModel(), "fa-magnifying-glass") { IsPrimary = false }
        ];

        SelectedSection = Sections.OfType<Section>().Skip(1).First();
    }

    public IEnumerable<SectionBase> Sections { get; }
}

public class Separator : SectionBase;

public class Section(string name, object viewModel, object? icon = null) : SectionBase
{
    public string Name { get; } = name;
    public object ViewModel { get; } = viewModel;
    public object? Icon { get; } = icon;
}
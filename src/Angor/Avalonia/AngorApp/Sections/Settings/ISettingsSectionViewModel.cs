using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace AngorApp.Sections.Settings;

internal interface ISettingsSectionViewModel : IDisposable
{
    ObservableCollection<SettingsUrlViewModel> Explorers { get; }
    ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    ObservableCollection<SettingsUrlViewModel> Relays { get; }
    IReadOnlyList<string> Networks { get; }
    string Network { get; set; }
    string NewExplorer { get; set; }
    string NewIndexer { get; set; }
    string NewRelay { get; set; }
    ReactiveCommand<Unit, Unit> AddExplorer { get; }
    ReactiveCommand<Unit, Unit> AddIndexer { get; }
    ReactiveCommand<Unit, Unit> AddRelay { get; }
}

internal class SettingsSectionViewModelDesign : ISettingsSectionViewModel
{
    public SettingsSectionViewModelDesign()
    {
        Explorers = new ObservableCollection<SettingsUrlViewModel>
        {
            new("https://explorer.angor.io", true, _ => { }, _ => { })
        };
        Indexers = new ObservableCollection<SettingsUrlViewModel>
        {
            new("https://indexer.angor.io", true, _ => { }, _ => { })
        };
        Relays = new ObservableCollection<SettingsUrlViewModel>
        {
            new("wss://relay.angor.io", false, _ => { })
        };
    }

    public ObservableCollection<SettingsUrlViewModel> Explorers { get; }
    public ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    public ObservableCollection<SettingsUrlViewModel> Relays { get; }
    public IReadOnlyList<string> Networks { get; } = new[] { "Angornet", "Mainnet" };
    public string Network { get; set; } = "Angornet";
    public string NewExplorer { get; set; } = string.Empty;
    public string NewIndexer { get; set; } = string.Empty;
    public string NewRelay { get; set; } = string.Empty;
    public ReactiveCommand<Unit, Unit> AddExplorer { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> AddIndexer { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> AddRelay { get; } = ReactiveCommand.Create(() => { });
    public void Dispose() { }
}

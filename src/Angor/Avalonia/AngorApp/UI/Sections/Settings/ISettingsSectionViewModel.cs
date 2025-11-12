using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace AngorApp.UI.Sections.Settings;

internal interface ISettingsSectionViewModel : IDisposable
{
    ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    ObservableCollection<SettingsUrlViewModel> Relays { get; }
    IReadOnlyList<string> Networks { get; }
    string Network { get; set; }
    string NewIndexer { get; set; }
    string NewRelay { get; set; }
    ReactiveCommand<Unit, Unit> AddIndexer { get; }
    ReactiveCommand<Unit, Unit> AddRelay { get; }
    ReactiveCommand<Unit, Unit> DeleteWallet { get; }
    bool IsBitcoinPreferred { get; set; }
}

internal class SettingsSectionViewModelSample : ISettingsSectionViewModel
{
    public SettingsSectionViewModelSample()
    {
        Indexers = new ObservableCollection<SettingsUrlViewModel>
        {
            new("https://indexer.angor.io", true, _ => { }, _ => { })
        };
        Relays = new ObservableCollection<SettingsUrlViewModel>
        {
            new("wss://relay.angor.io", false, _ => { })
        };
    }

    public ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    public ObservableCollection<SettingsUrlViewModel> Relays { get; }
    public IReadOnlyList<string> Networks { get; } = new[] { "Angornet", "Mainnet" };
    public string Network { get; set; } = "Angornet";
    public string NewIndexer { get; set; } = string.Empty;
    public string NewRelay { get; set; } = string.Empty;
    public ReactiveCommand<Unit, Unit> AddIndexer { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> AddRelay { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> DeleteWallet { get; } = ReactiveCommand.Create(() => { });
    public bool IsBitcoinPreferred { get; set; } = true;
    public void Dispose() { }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Linq;
using Angor.Shared.Models;
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
    string? SelectedIndexerUri { get; set; }
    ReactiveCommand<Unit, Unit> AddIndexer { get; }
    ReactiveCommand<Unit, Unit> AddRelay { get; }
    ReactiveCommand<Unit, Unit> RefreshIndexers { get; }
    ReactiveCommand<Unit, Unit> DeleteWallet { get; }
    bool IsBitcoinPreferred { get; set; }
    bool IsDebugMode { get; set; }
    bool IsTestnet { get; }
}

internal class SettingsSectionViewModelSample : ISettingsSectionViewModel
{
    public SettingsSectionViewModelSample()
    {
        Indexers = new ObservableCollection<SettingsUrlViewModel>
        {
            new("https://indexer.angor.io", true, UrlStatus.Online, DateTime.UtcNow, _ => { }, _ => { })
        };
        Relays = new ObservableCollection<SettingsUrlViewModel>
        {
            new("wss://relay.angor.io", false, UrlStatus.Offline, DateTime.UtcNow, _ => { })
        };
        SelectedIndexerUri = Indexers.First().Url;
    }

    public ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    public ObservableCollection<SettingsUrlViewModel> Relays { get; }
    public IReadOnlyList<string> Networks { get; } = new[] { "Angornet", "Mainnet" };
    public string Network { get; set; } = "Angornet";
    public string NewIndexer { get; set; } = string.Empty;
    public string NewRelay { get; set; } = string.Empty;
    public ReactiveCommand<Unit, Unit> AddIndexer { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> AddRelay { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> RefreshIndexers { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> DeleteWallet { get; } = ReactiveCommand.Create(() => { });
    public bool IsBitcoinPreferred { get; set; } = true;
    public bool IsDebugMode { get; set; } = false;
    public bool IsTestnet { get; } = true;
    public string? SelectedIndexerUri { get; set; }
    public void Dispose() { }
}

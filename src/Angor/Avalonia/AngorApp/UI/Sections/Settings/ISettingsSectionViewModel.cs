using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using Angor.Shared.Models;
using AngorApp.UI.Shared.Controls;
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
    ReactiveCommand<Unit, Unit> RefreshIndexers { get; }
    ReactiveCommand<Unit, Unit> RefreshRelays { get; }
    ReactiveCommand<Unit, Unit> ChangeNetwork { get; }
    ReactiveCommand<Unit, Unit> WipeData { get; }
    ReactiveCommand<Unit, Unit> BackupWallet { get; }
    IEnhancedCommand ImportWallet { get; }
    bool IsBitcoinPreferred { get; set; }
    bool IsDebugMode { get; set; }
    bool IsTestnet { get; }
    bool HasWallet { get; }
}

internal class SettingsSectionViewModelSample : ISettingsSectionViewModel
{
    public SettingsSectionViewModelSample()
    {
        Indexers = new ObservableCollection<SettingsUrlViewModel>
        {
            new("https://test.indexer.angor.io", false, UrlStatus.Offline, DateTime.UtcNow, _ => { }, _ => { }),
            new("https://signet.angor.online", true, UrlStatus.Online, DateTime.UtcNow, _ => { }, _ => { }),
            new("https://signet2.angor.online", false, UrlStatus.Offline, DateTime.UtcNow, _ => { }, _ => { })
        };
        Relays = new ObservableCollection<SettingsUrlViewModel>
        {
            new("wss://relay.angor.io", false, UrlStatus.Online, DateTime.UtcNow, _ => { }, name: "strfry default"),
            new("wss://relay2.angor.io", false, UrlStatus.Online, DateTime.UtcNow, _ => { }, name: "strfry2 default")
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
    public ReactiveCommand<Unit, Unit> RefreshIndexers { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> RefreshRelays { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> ChangeNetwork { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> WipeData { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> BackupWallet { get; } = ReactiveCommand.Create(() => { });
    public IEnhancedCommand ImportWallet { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public bool IsBitcoinPreferred { get; set; } = true;
    public bool IsDebugMode { get; set; } = false;
    public bool IsTestnet { get; } = true;
    public bool HasWallet { get; } = true;
    public void Dispose() { }
}

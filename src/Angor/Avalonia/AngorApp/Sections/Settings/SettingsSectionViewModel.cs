using System.Collections.ObjectModel;
using System.Collections.Generic;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using System.Linq;
using AngorApp.UI.Services;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Settings;

public partial class SettingsSectionViewModel : ReactiveObject, ISettingsSectionViewModel, IDisposable
{
    private readonly INetworkStorage networkStorage;
    private readonly IWalletStore walletStore;
    private readonly UIServices uiServices;
    private readonly INetworkService networkService;
    private readonly INetworkConfiguration networkConfiguration;
    private string currentNetwork;

    private string network;
    private string newExplorer;
    private string newIndexer;
    private string newRelay;

    private readonly IDisposable networkChanged;

    public SettingsSectionViewModel(INetworkStorage networkStorage, IWalletStore walletStore, UIServices uiServices, INetworkService networkService, INetworkConfiguration networkConfiguration)
    {
        this.networkStorage = networkStorage;
        this.walletStore = walletStore;
        this.uiServices = uiServices;
        this.networkService = networkService;
        this.networkConfiguration = networkConfiguration;

        networkService.AddSettingsIfNotExist();

        var settings = networkStorage.GetSettings();
        Explorers = new ObservableCollection<SettingsUrlViewModel>(settings.Explorers.Select(CreateExplorer));
        Indexers = new ObservableCollection<SettingsUrlViewModel>(settings.Indexers.Select(CreateIndexer));
        Relays = new ObservableCollection<SettingsUrlViewModel>(settings.Relays.Select(CreateRelay));

        currentNetwork = networkStorage.GetNetwork();
        networkConfiguration.SetNetwork(currentNetwork == "Mainnet" ? new BitcoinMain() : new Angornet());
        Network = currentNetwork;

        AddExplorer = ReactiveCommand.Create(AddExplorerImpl, this.WhenAnyValue(x => x.NewExplorer, url => !string.IsNullOrWhiteSpace(url)));
        AddIndexer = ReactiveCommand.Create(AddIndexerImpl, this.WhenAnyValue(x => x.NewIndexer, url => !string.IsNullOrWhiteSpace(url)));
        AddRelay = ReactiveCommand.Create(AddRelayImpl, this.WhenAnyValue(x => x.NewRelay, url => !string.IsNullOrWhiteSpace(url)));

        networkChanged = this.WhenAnyValue(x => x.Network)
            .Skip(1)
            .SelectMany(async n => (n, await uiServices.Dialog.ShowConfirmation("Change network?", "Changing network will delete the current wallet")))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(t => t.Item2.Match(
                confirmed =>
                {
                    if (confirmed)
                    {
                        networkStorage.SetNetwork(t.n);
                        networkStorage.SetSettings(new SettingsInfo());
                        networkConfiguration.SetNetwork(t.n == "Mainnet" ? new BitcoinMain() : new Angornet());
                        networkService.AddSettingsIfNotExist();
                        var s = networkStorage.GetSettings();
                        Reset(Explorers, s.Explorers.Select(CreateExplorer));
                        Reset(Indexers, s.Indexers.Select(CreateIndexer));
                        Reset(Relays, s.Relays.Select(CreateRelay));
                        walletStore.SaveAll(Enumerable.Empty<EncryptedWallet>());
                        currentNetwork = t.n;
                    }
                    else
                    {
                        Network = currentNetwork;
                    }
                    return Unit.Default;
                },
                () =>
                {
                    Network = currentNetwork;
                    return Unit.Default;
                }));

        Save = ReactiveCommand.Create(SaveSettings);

        this.WhenAnyObservable(x => x.AddExplorer, x => x.AddIndexer, x => x.AddRelay)
            .InvokeCommand(Save);
    }

    public ObservableCollection<SettingsUrlViewModel> Explorers { get; }
    public ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    public ObservableCollection<SettingsUrlViewModel> Relays { get; }

    public IReadOnlyList<string> Networks { get; } = new[] { "Angornet", "Mainnet" };

    public ReactiveCommand<Unit, Unit> AddExplorer { get; }
    public ReactiveCommand<Unit, Unit> AddIndexer { get; }
    public ReactiveCommand<Unit, Unit> AddRelay { get; }

    public ReactiveCommand<Unit, Unit> Save { get; }

    public string Network
    {
        get => network;
        set => this.RaiseAndSetIfChanged(ref network, value);
    }

    public string NewExplorer
    {
        get => newExplorer;
        set => this.RaiseAndSetIfChanged(ref newExplorer, value);
    }

    public string NewIndexer
    {
        get => newIndexer;
        set => this.RaiseAndSetIfChanged(ref newIndexer, value);
    }

    public string NewRelay
    {
        get => newRelay;
        set => this.RaiseAndSetIfChanged(ref newRelay, value);
    }

    void AddExplorerImpl()
    {
        Explorers.Add(CreateExplorer(new SettingsUrl { Url = NewExplorer, IsPrimary = Explorers.Count == 0 }));
        NewExplorer = string.Empty;
        Refresh(Explorers);
    }

    void AddIndexerImpl()
    {
        Indexers.Add(CreateIndexer(new SettingsUrl { Url = NewIndexer, IsPrimary = Indexers.Count == 0 }));
        NewIndexer = string.Empty;
        Refresh(Indexers);
    }

    void AddRelayImpl()
    {
        Relays.Add(CreateRelay(new SettingsUrl { Url = NewRelay }));
        NewRelay = string.Empty;
        Refresh(Relays);
    }

    SettingsUrlViewModel CreateExplorer(SettingsUrl url) => new(url.Url, url.IsPrimary, RemoveExplorerImpl, SetPrimaryExplorerImpl);
    SettingsUrlViewModel CreateIndexer(SettingsUrl url) => new(url.Url, url.IsPrimary, RemoveIndexerImpl, SetPrimaryIndexerImpl);
    SettingsUrlViewModel CreateRelay(SettingsUrl url) => new(url.Url, url.IsPrimary, RemoveRelayImpl);

    void RemoveExplorerImpl(SettingsUrlViewModel url)
    {
        var wasPrimary = url.IsPrimary;
        Explorers.Remove(url);
        if (wasPrimary && Explorers.Count > 0)
        {
            Explorers[0].IsPrimary = true;
        }
        Refresh(Explorers);
    }

    void SetPrimaryExplorerImpl(SettingsUrlViewModel url)
    {
        foreach (var e in Explorers)
        {
            e.IsPrimary = false;
        }
        url.IsPrimary = true;
        Refresh(Explorers);
    }

    void RemoveIndexerImpl(SettingsUrlViewModel url)
    {
        var wasPrimary = url.IsPrimary;
        Indexers.Remove(url);
        if (wasPrimary && Indexers.Count > 0)
        {
            Indexers[0].IsPrimary = true;
        }
        Refresh(Indexers);
    }

    void SetPrimaryIndexerImpl(SettingsUrlViewModel url)
    {
        foreach (var e in Indexers)
        {
            e.IsPrimary = false;
        }
        url.IsPrimary = true;
        Refresh(Indexers);
    }

    void RemoveRelayImpl(SettingsUrlViewModel url)
    {
        Relays.Remove(url);
        Refresh(Relays);
    }

    static void Refresh(ObservableCollection<SettingsUrlViewModel> collection)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            var item = collection[i];
            collection[i] = item;
        }
    }

    static void Reset(ObservableCollection<SettingsUrlViewModel> collection, IEnumerable<SettingsUrlViewModel> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
        Refresh(collection);
    }

    void SaveSettings()
    {
        var info = new SettingsInfo
        {
            Explorers = Explorers.Select(x => x.ToModel()).ToList(),
            Indexers = Indexers.Select(x => x.ToModel()).ToList(),
            Relays = Relays.Select(x => x.ToModel()).ToList()
        };
        networkStorage.SetSettings(info);
    }

    public void Dispose()
    {
        AddExplorer.Dispose();
        AddIndexer.Dispose();
        AddRelay.Dispose();
        Save.Dispose();
        networkChanged.Dispose();
    }
}


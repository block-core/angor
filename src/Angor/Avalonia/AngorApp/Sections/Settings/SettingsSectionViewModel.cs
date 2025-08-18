using System.Collections.ObjectModel;
using System.Collections.Generic;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using System.Linq;
using System.Reactive.Disposables;
using AngorApp.UI.Services;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Settings;

public partial class SettingsSectionViewModel : ReactiveObject, ISettingsSectionViewModel
{
    private readonly INetworkStorage networkStorage;

    private string network;
    private string newExplorer;
    private string newIndexer;
    private string newRelay;

    private readonly CompositeDisposable disposable = new();
    
    public SettingsSectionViewModel(INetworkStorage networkStorage, IWalletStore walletStore, UIServices uiServices, INetworkService networkService, INetworkConfiguration networkConfiguration)
    {
        this.networkStorage = networkStorage;

        networkService.AddSettingsIfNotExist();

        var settings = networkStorage.GetSettings();
        Explorers = new ObservableCollection<SettingsUrlViewModel>(settings.Explorers.Select(CreateExplorer));
        Indexers = new ObservableCollection<SettingsUrlViewModel>(settings.Indexers.Select(CreateIndexer));
        Relays = new ObservableCollection<SettingsUrlViewModel>(settings.Relays.Select(CreateRelay));

        var currentNetwork = networkStorage.GetNetwork();
        networkConfiguration.SetNetwork(currentNetwork == "Mainnet" ? new BitcoinMain() : new Angornet());
        Network = currentNetwork;

        AddExplorer = ReactiveCommand.Create(DoAddExplorer, this.WhenAnyValue(x => x.NewExplorer, url => !string.IsNullOrWhiteSpace(url))).DisposeWith(disposable);;
        AddIndexer = ReactiveCommand.Create(DoAddIndexer, this.WhenAnyValue(x => x.NewIndexer, url => !string.IsNullOrWhiteSpace(url))).DisposeWith(disposable);;
        AddRelay = ReactiveCommand.Create(DoAddRelay, this.WhenAnyValue(x => x.NewRelay, url => !string.IsNullOrWhiteSpace(url))).DisposeWith(disposable);

        this.WhenAnyValue(x => x.Network)
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
                        walletStore.SaveAll([]);
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
                }))
            .DisposeWith(disposable);

        Save = ReactiveCommand.Create(SaveSettings).DisposeWith(disposable);

        this.WhenAnyObservable(x => x.AddExplorer, x => x.AddIndexer, x => x.AddRelay)
            .InvokeCommand(Save)
            .DisposeWith(disposable);
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

    private void DoAddExplorer()
    {
        Explorers.Add(CreateExplorer(new SettingsUrl { Url = NewExplorer, IsPrimary = Explorers.Count == 0 }));
        NewExplorer = string.Empty;
        Refresh(Explorers);
    }

    private void DoAddIndexer()
    {
        Indexers.Add(CreateIndexer(new SettingsUrl { Url = NewIndexer, IsPrimary = Indexers.Count == 0 }));
        NewIndexer = string.Empty;
        Refresh(Indexers);
    }

    private void DoAddRelay()
    {
        Relays.Add(CreateRelay(new SettingsUrl { Url = NewRelay }));
        NewRelay = string.Empty;
        Refresh(Relays);
    }

    private SettingsUrlViewModel CreateExplorer(SettingsUrl url) => new(url.Url, url.IsPrimary, DoRemoveExplorer, DoSetPrimaryExplorer);
    private SettingsUrlViewModel CreateIndexer(SettingsUrl url) => new(url.Url, url.IsPrimary, DoRemoveIndexer, DoSetPrimaryIndexer);
    private SettingsUrlViewModel CreateRelay(SettingsUrl url) => new(url.Url, url.IsPrimary, DoRemoveRelay);

    private void DoRemoveExplorer(SettingsUrlViewModel url)
    {
        var wasPrimary = url.IsPrimary;
        Explorers.Remove(url);
        if (wasPrimary && Explorers.Count > 0)
        {
            Explorers[0].IsPrimary = true;
        }
        Refresh(Explorers);
    }

    private void DoSetPrimaryExplorer(SettingsUrlViewModel url)
    {
        foreach (var e in Explorers)
        {
            e.IsPrimary = false;
        }
        url.IsPrimary = true;
        Refresh(Explorers);
    }

    private void DoRemoveIndexer(SettingsUrlViewModel url)
    {
        var wasPrimary = url.IsPrimary;
        Indexers.Remove(url);
        if (wasPrimary && Indexers.Count > 0)
        {
            Indexers[0].IsPrimary = true;
        }
        Refresh(Indexers);
    }

    private void DoSetPrimaryIndexer(SettingsUrlViewModel url)
    {
        foreach (var e in Indexers)
        {
            e.IsPrimary = false;
        }
        url.IsPrimary = true;
        Refresh(Indexers);
    }

    private void DoRemoveRelay(SettingsUrlViewModel url)
    {
        Relays.Remove(url);
        Refresh(Relays);
    }

    private static void Refresh(ObservableCollection<SettingsUrlViewModel> collection)
    {
        for (var i = 0; i < collection.Count; i++)
        {
            var item = collection[i];
            collection[i] = item;
        }
    }

    private static void Reset(ObservableCollection<SettingsUrlViewModel> collection, IEnumerable<SettingsUrlViewModel> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
        Refresh(collection);
    }

    private void SaveSettings()
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
        disposable.Dispose();
    }
}
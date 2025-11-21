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
using AngorApp.UI.Shared.Controls;
using AngorApp.UI.Shared.Services;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using ReactiveUI;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Settings;

public partial class SettingsSectionViewModel : ReactiveObject, ISettingsSectionViewModel
{
    [Reactive]
    private bool isBitcoinPreferred;

    private readonly INetworkStorage networkStorage;

    private readonly IWalletStore walletStore;

    private readonly UIServices uiServices;

    private readonly IWalletContext walletContext;

    private readonly INetworkConfiguration networkConfiguration;

    private readonly INetworkService networkService;

    private string network;

    private string newIndexer;

    private string newRelay;

    private bool restoringNetwork;

    private string currentNetwork;

    private bool isDebugMode;

    private bool isTestnet;

    private readonly CompositeDisposable disposable = new();

    public SettingsSectionViewModel(INetworkStorage networkStorage, IWalletStore walletStore, UIServices uiServices, INetworkService networkService, INetworkConfiguration networkConfiguration, IWalletContext walletContext, WalletImportWizard walletImportWizard)
    {
        this.networkStorage = networkStorage;
        this.walletStore = walletStore;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
        this.networkConfiguration = networkConfiguration;
        this.networkService = networkService;

        this.networkService.AddSettingsIfNotExist();

        var settings = networkStorage.GetSettings();
        Indexers = new ObservableCollection<SettingsUrlViewModel>(settings.Indexers.Select(CreateIndexer));
        Relays = new ObservableCollection<SettingsUrlViewModel>(settings.Relays.Select(CreateRelay));
        currentNetwork = networkStorage.GetNetwork();
        networkConfiguration.SetNetwork(currentNetwork == "Mainnet" ? new BitcoinMain() : new Angornet());
        Network = currentNetwork;
        IsTestnet = currentNetwork == "Angornet";

        AddIndexer = ReactiveCommand.Create(DoAddIndexer, this.WhenAnyValue(x => x.NewIndexer, url => !string.IsNullOrWhiteSpace(url))).DisposeWith(disposable);
        AddRelay = ReactiveCommand.Create(DoAddRelay, this.WhenAnyValue(x => x.NewRelay, url => !string.IsNullOrWhiteSpace(url))).DisposeWith(disposable);
        RefreshIndexers = ReactiveCommand.CreateFromTask(RefreshIndexersAsync).DisposeWith(disposable);
        ImportWallet = ReactiveCommand.CreateFromTask(walletImportWizard.Start).Enhance().DisposeWith(disposable);

        var canDeleteWallet = walletContext.CurrentWalletChanges
            .Select(maybe => maybe.HasValue)
            .StartWith(walletContext.CurrentWallet.HasValue)
            .ObserveOn(RxApp.MainThreadScheduler);
        DeleteWallet = ReactiveCommand.CreateFromTask(DeleteWalletAsync, canDeleteWallet).DisposeWith(disposable);

        this.WhenAnyValue(x => x.Network)
            .Skip(1)
            .Where(_ => !restoringNetwork)
            .SelectMany(async n => (n, await this.uiServices.Dialog.ShowConfirmation("Change network?", "Changing network will delete the current wallet")))
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
                        Reset(Indexers, s.Indexers.Select(CreateIndexer));
                        Reset(Relays, s.Relays.Select(CreateRelay));
                        this.walletStore.SaveAll([]);
                        currentNetwork = t.n;
                    }
                    else
                    {
                        RestoreNetwork();
                    }
                    return Unit.Default;
                },
                () =>
                {
                    RestoreNetwork();
                    return Unit.Default;
                }))
            .DisposeWith(disposable);

        IsBitcoinPreferred = uiServices.IsBitcoinPreferred;
        this.WhenAnyValue(model => model.IsBitcoinPreferred)
            .BindTo(uiServices, services => services.IsBitcoinPreferred)
            .DisposeWith(disposable);

        IsDebugMode = uiServices.IsDebugModeEnabled;
        this.WhenAnyValue(x => x.IsDebugMode)
            .Skip(1)
            .BindTo(uiServices, services => services.IsDebugModeEnabled)
            .DisposeWith(disposable);
    }

    public ObservableCollection<SettingsUrlViewModel> Indexers { get; }
    public ObservableCollection<SettingsUrlViewModel> Relays { get; }

    public IReadOnlyList<string> Networks { get; } = new[] { "Angornet", "Mainnet" };

    public ReactiveCommand<Unit, Unit> AddIndexer { get; }
    public ReactiveCommand<Unit, Unit> AddRelay { get; }
    public ReactiveCommand<Unit, Unit> RefreshIndexers { get; }
    public ReactiveCommand<Unit, Unit> DeleteWallet { get; }
    public IEnhancedCommand ImportWallet { get; }

    public string Network
    {
        get => network;
        set
        {
            this.RaiseAndSetIfChanged(ref network, value);
            IsTestnet = value == "Angornet";
        }
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

    public bool IsDebugMode
    {
        get => isDebugMode;
        set => this.RaiseAndSetIfChanged(ref isDebugMode, value);
    }

    public bool IsTestnet
    {
        get => isTestnet;
        private set => this.RaiseAndSetIfChanged(ref isTestnet, value);
    }

    private void DoAddIndexer()
    {
        Indexers.Add(CreateIndexer(new SettingsUrl
        {
            Url = NewIndexer,
            IsPrimary = Indexers.Count == 0,
            Status = UrlStatus.NotReady,
            LastCheck = DateTime.UtcNow
        }));
        NewIndexer = string.Empty;
        Refresh(Indexers);
        SaveSettings();
    }

    private void DoAddRelay()
    {
        Relays.Add(CreateRelay(new SettingsUrl
        {
            Url = NewRelay,
            Status = UrlStatus.NotReady,
            LastCheck = DateTime.UtcNow
        }));
        NewRelay = string.Empty;
        Refresh(Relays);
        SaveSettings();
    }

    private SettingsUrlViewModel CreateIndexer(SettingsUrl url) => new(url.Url, url.IsPrimary, url.Status, url.LastCheck, DoRemoveIndexer, DoSetPrimaryIndexer, url.Name);
    private SettingsUrlViewModel CreateRelay(SettingsUrl url) => new(url.Url, url.IsPrimary, url.Status, url.LastCheck, DoRemoveRelay, name: url.Name);

    private void DoRemoveIndexer(SettingsUrlViewModel url)
    {
        var wasPrimary = url.IsPrimary;
        Indexers.Remove(url);
        if (wasPrimary && Indexers.Count > 0)
        {
            Indexers[0].IsPrimary = true;
        }
        Refresh(Indexers);
        SaveSettings();
    }

    private void DoSetPrimaryIndexer(SettingsUrlViewModel url)
    {
        foreach (var e in Indexers)
        {
            e.IsPrimary = false;
        }
        url.IsPrimary = true;
        Refresh(Indexers);
        SaveSettings();
    }

    private void DoRemoveRelay(SettingsUrlViewModel url)
    {
        Relays.Remove(url);
        Refresh(Relays);
        SaveSettings();
    }

    private async Task RefreshIndexersAsync()
    {
        try
        {
            await networkService.CheckServices(true);
        }
        catch (Exception ex)
        {
            await uiServices.Dialog.ShowMessage("Indexer refresh failed", ex.Message);
        }

        var settings = networkStorage.GetSettings();
        Reset(Indexers, settings.Indexers.Select(CreateIndexer));
    }

    private async Task DeleteWalletAsync()
    {
        var confirmation = await uiServices.Dialog.ShowConfirmation("Delete wallet?", "Deleting the current wallet will remove all local wallet data. This action cannot be undone.");
        var shouldDelete = confirmation.GetValueOrDefault(() => false);

        if (!shouldDelete)
        {
            return;
        }

        var wallet = walletContext.CurrentWallet.GetValueOrDefault();
        if (wallet is null)
        {
            return;
        }

        var deleteResult = await walletContext.DeleteWallet(wallet.Id);
        if (deleteResult.IsFailure)
        {
            await uiServices.Dialog.ShowMessage("Delete wallet failed", deleteResult.Error);
            return;
        }

        await uiServices.Dialog.ShowMessage("Wallet deleted", "The current wallet has been removed.");
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
        var current = networkStorage.GetSettings();
        current.Indexers = Indexers.Select(x => x.ToModel()).ToList();
        current.Relays = Relays.Select(x => x.ToModel()).ToList();
        networkStorage.SetSettings(current);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
    
    void RestoreNetwork()
    {
        restoringNetwork = true;
        Network = currentNetwork;
        restoringNetwork = false;
    }
}

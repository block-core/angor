using System.Collections.ObjectModel;
using System.Reactive;
using Angor.Sdk.Wallet.Application;
using Avalonia2.Composition;
using Avalonia2.UI.Shared;
using ReactiveUI;

namespace Avalonia2.UI.Sections.MyProjects.Deploy;

/// <summary>Which screen the deploy overlay is showing.</summary>
public enum DeployScreen
{
    WalletSelector,
    PayFee,
    Success
}

/// <summary>Wallet item for the wallet selector list.</summary>
public partial class WalletItem : ReactiveObject
{
    public string Name { get; set; } = "";
    public string Network { get; set; } = "Bitcoin";
    public string Balance { get; set; } = "0.00000000 BTC";
    /// <summary>SDK WalletId for operations</summary>
    public string WalletId { get; set; } = "";

    [Reactive] private bool isSelected;
}

/// <summary>
/// ViewModel for the deploy flow overlay.
/// Orchestrates: Wallet Selector → Pay Fee → Success.
/// Connected to SDK for wallet loading.
/// </summary>
public partial class DeployFlowViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;

    // ── State ──
    [Reactive] private DeployScreen currentScreen;
    [Reactive] private bool isVisible;
    [Reactive] private WalletItem? selectedWallet;
    [Reactive] private bool isDeploying;
    [Reactive] private string deployStatusText = "Waiting for payment...";

    // ── Derived visibility ──
    public bool IsWalletSelector => CurrentScreen == DeployScreen.WalletSelector;
    public bool IsPayFee => CurrentScreen == DeployScreen.PayFee;
    public bool IsSuccess => CurrentScreen == DeployScreen.Success;
    public bool HasSelectedWallet => SelectedWallet != null;
    public string PayButtonText => SelectedWallet != null
        ? $"Pay with {SelectedWallet.Name}"
        : "Choose Wallet";

    public string DeployFee { get; } = "0.0001 BTC";
    public string InvoiceString { get; } = Constants.InvoiceString;

    public string ProjectName { get; set; } = "My Project";

    /// <summary>Callback when deploy flow completes successfully.</summary>
    public Action? OnDeployCompleted { get; set; }

    // ── Wallets loaded from SDK ──
    public ObservableCollection<WalletItem> Wallets { get; } = new();

    public DeployFlowViewModel()
    {
        _walletAppService = ServiceLocator.WalletApp;
        // Initialize ReactiveCommands for async payment operations
        PayWithWalletCommand = ReactiveCommand.CreateFromTask(PayWithWalletAsync);
        PayViaInvoiceCommand = ReactiveCommand.CreateFromTask(PayViaInvoiceAsync);

        // Raise derived property notifications when screen changes
        this.WhenAnyValue(x => x.CurrentScreen)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsWalletSelector));
                this.RaisePropertyChanged(nameof(IsPayFee));
                this.RaisePropertyChanged(nameof(IsSuccess));
            });

        this.WhenAnyValue(x => x.SelectedWallet)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(HasSelectedWallet));
                this.RaisePropertyChanged(nameof(PayButtonText));
            });
    }

    /// <summary>Show the deploy flow starting at wallet selector.</summary>
    public void Show(string projectName)
    {
        ProjectName = projectName;
        this.RaisePropertyChanged(nameof(ProjectName));
        CurrentScreen = DeployScreen.WalletSelector;
        SelectedWallet = null;
        foreach (var w in Wallets) w.IsSelected = false;
        IsDeploying = false;
        DeployStatusText = "Waiting for payment...";
        IsVisible = true;

        // Load wallets from SDK
        _ = LoadWalletsAsync();
    }

    /// <summary>Load wallets from SDK for the wallet selector.</summary>
    private async Task LoadWalletsAsync()
    {
        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure) return;

            Wallets.Clear();
            foreach (var meta in metadatasResult.Value)
            {
                var balanceResult = await _walletAppService.GetBalance(meta.Id);
                var balanceBtc = balanceResult.IsSuccess ? balanceResult.Value.Sats / 100_000_000.0 : 0;

                Wallets.Add(new WalletItem
                {
                    Name = meta.Name,
                    Network = "Bitcoin",
                    Balance = $"{balanceBtc:F8} BTC",
                    WalletId = meta.Id.Value
                });
            }
        }
        catch
        {
            // Wallet loading failed
        }
    }

    /// <summary>Close the overlay without completing.
    /// Vue ref: closeDeployModal() resets all state.
    /// Wallet picker close: showDeployWalletModal = false (just dismiss).
    /// This is for X buttons and non-success dismissal only.</summary>
    public void Close()
    {
        IsVisible = false;
    }

    /// <summary>Select a wallet from the list.</summary>
    public void SelectWallet(WalletItem wallet)
    {
        foreach (var w in Wallets) w.IsSelected = false;
        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    /// <summary>Pay with selected wallet — simulate deploy.
    /// Vue ref: payWithDeployWallet() → 800ms spinner → QR "received" → 1500ms "Deploying..." → success.</summary>
    public ReactiveCommand<Unit, Unit> PayWithWalletCommand { get; }

    public void PayWithWallet() => PayWithWalletCommand.Execute().Subscribe();

    private async Task PayWithWalletAsync()
    {
        if (SelectedWallet == null) return;
        IsDeploying = true;
        DeployStatusText = "Deploying...";
        await Task.Delay(2500); // simulate payment + deploy
        CurrentScreen = DeployScreen.Success;
        IsDeploying = false;
    }

    /// <summary>Switch to the invoice/pay fee screen.
    /// Vue ref: proceedToDeployInvoice() → showDeployModal with QR view.</summary>
    public void ShowPayFee()
    {
        CurrentScreen = DeployScreen.PayFee;
    }

    /// <summary>Go back to wallet selector from pay fee.</summary>
    public void BackToWalletSelector()
    {
        CurrentScreen = DeployScreen.WalletSelector;
    }

    /// <summary>Simulate paying via invoice.
    /// Vue ref: handlePayment() → paymentStatus "received" → 1500ms → success.</summary>
    public ReactiveCommand<Unit, Unit> PayViaInvoiceCommand { get; }

    public void PayViaInvoice() => PayViaInvoiceCommand.Execute().Subscribe();

    private async Task PayViaInvoiceAsync()
    {
        IsDeploying = true;
        DeployStatusText = "Deploying...";
        await Task.Delay(3000);
        CurrentScreen = DeployScreen.Success;
        IsDeploying = false;
    }

    /// <summary>Complete the flow — go to my projects.
    /// Vue ref: goToMyProjects() creates project, adds to list, closes wizard, navigates to my-projects.
    /// Both "Go to My Projects" button AND backdrop click on success modal call this.</summary>
    public void GoToMyProjects()
    {
        IsVisible = false;
        OnDeployCompleted?.Invoke();
    }
}

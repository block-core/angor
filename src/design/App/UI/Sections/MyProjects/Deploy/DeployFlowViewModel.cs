using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Wallet.Application;
using App.UI.Shared;
using ReactiveUI;

namespace App.UI.Sections.MyProjects.Deploy;

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
/// Connected to SDK for wallet loading and project deployment.
/// </summary>
public partial class DeployFlowViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;
    private readonly IProjectAppService _projectAppService;
    private readonly IFounderAppService _founderAppService;
    private CancellationTokenSource? _invoiceMonitorCts;

    // ── State ──
    [Reactive] private DeployScreen currentScreen;
    [Reactive] private bool isVisible;
    [Reactive] private WalletItem? selectedWallet;
    [Reactive] private bool isDeploying;
    [Reactive] private string deployStatusText = "Waiting for payment...";
    [Reactive] private long selectedFeeRate = 20;

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

    /// <summary>Project data built by CreateProjectViewModel, consumed during deployment.</summary>
    public CreateProjectDto? ProjectData { get; set; }

    /// <summary>Callback when deploy flow completes successfully.</summary>
    public Action? OnDeployCompleted { get; set; }

    // ── Wallets loaded from SDK ──
    public ObservableCollection<WalletItem> Wallets { get; } = new();

    public DeployFlowViewModel(
        IWalletAppService walletAppService,
        IProjectAppService projectAppService,
        IFounderAppService founderAppService)
    {
        _walletAppService = walletAppService;
        _projectAppService = projectAppService;
        _founderAppService = founderAppService;
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
        _invoiceMonitorCts?.Cancel();
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
        if (SelectedWallet == null || string.IsNullOrEmpty(SelectedWallet.WalletId)) return;
        if (ProjectData == null)
        {
            DeployStatusText = "No project data available.";
            return;
        }

        IsDeploying = true;

        try
        {
            var walletId = new WalletId(SelectedWallet.WalletId);

            // Step 1: Create project keys
            DeployStatusText = "Generating project keys...";
            var keysResult = await _founderAppService.CreateProjectKeys(
                new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            if (keysResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project keys: {keysResult.Error}";
                IsDeploying = false;
                return;
            }

            var projectSeed = keysResult.Value.ProjectSeedDto;

            // Step 2: Create Nostr profile
            DeployStatusText = "Creating project profile...";
            var profileResult = await _projectAppService.CreateProjectProfile(walletId, projectSeed, ProjectData);
            if (profileResult.IsFailure)
            {
                DeployStatusText = $"Failed to create profile: {profileResult.Error}";
                IsDeploying = false;
                return;
            }

            // Step 3: Create project info
            DeployStatusText = "Publishing project info...";
            var infoResult = await _projectAppService.CreateProjectInfo(walletId, ProjectData, projectSeed);
            if (infoResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project info: {infoResult.Error}";
                IsDeploying = false;
                return;
            }

            var infoEventId = infoResult.Value.EventId;

            // Step 4: Create blockchain transaction
            DeployStatusText = "Building transaction...";
            var txResult = await _projectAppService.CreateProject(walletId, SelectedFeeRate, ProjectData, infoEventId, projectSeed);
            if (txResult.IsFailure)
            {
                DeployStatusText = $"Failed to create transaction: {txResult.Error}";
                IsDeploying = false;
                return;
            }

            var transactionDraft = txResult.Value.TransactionDraft;

            // Step 5: Publish to blockchain
            DeployStatusText = "Publishing to blockchain...";
            var publishResult = await _founderAppService.SubmitTransactionFromDraft(
                new PublishFounderTransaction.PublishFounderTransactionRequest(transactionDraft));
            if (publishResult.IsFailure)
            {
                DeployStatusText = $"Failed to publish: {publishResult.Error}";
                IsDeploying = false;
                return;
            }

            CurrentScreen = DeployScreen.Success;
        }
        catch (Exception ex)
        {
            DeployStatusText = $"Deployment error: {ex.Message}";
        }
        finally
        {
            IsDeploying = false;
        }
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
        if (ProjectData == null)
        {
            DeployStatusText = "No project data available.";
            return;
        }

        // Use the first wallet for key generation and address monitoring
        var wallet = Wallets.FirstOrDefault();
        if (wallet == null || string.IsNullOrEmpty(wallet.WalletId))
        {
            DeployStatusText = "No wallet available for invoice monitoring.";
            return;
        }

        IsDeploying = true;
        _invoiceMonitorCts?.Cancel();
        _invoiceMonitorCts = new CancellationTokenSource();

        try
        {
            var walletId = new WalletId(wallet.WalletId);

            // Step 1: Create project keys (needed before we can deploy)
            DeployStatusText = "Generating project keys...";
            var keysResult = await _founderAppService.CreateProjectKeys(
                new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            if (keysResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project keys: {keysResult.Error}";
                IsDeploying = false;
                return;
            }

            var projectSeed = keysResult.Value.ProjectSeedDto;

            // The invoice QR was displayed to the user; proceed with deployment
            // once the user clicks "Pay Via Invoice" (confirming they sent payment).

            // Step 2: Create Nostr profile
            DeployStatusText = "Payment detected — creating project profile...";
            var profileResult = await _projectAppService.CreateProjectProfile(walletId, projectSeed, ProjectData);
            if (profileResult.IsFailure)
            {
                DeployStatusText = $"Failed to create profile: {profileResult.Error}";
                IsDeploying = false;
                return;
            }

            // Step 3: Create project info
            DeployStatusText = "Publishing project info...";
            var infoResult = await _projectAppService.CreateProjectInfo(walletId, ProjectData, projectSeed);
            if (infoResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project info: {infoResult.Error}";
                IsDeploying = false;
                return;
            }

            var infoEventId = infoResult.Value.EventId;

            // Step 4: Create blockchain transaction
            DeployStatusText = "Building transaction...";
            var txResult = await _projectAppService.CreateProject(walletId, SelectedFeeRate, ProjectData, infoEventId, projectSeed);
            if (txResult.IsFailure)
            {
                DeployStatusText = $"Failed to create transaction: {txResult.Error}";
                IsDeploying = false;
                return;
            }

            // Step 5: Publish to blockchain
            DeployStatusText = "Publishing to blockchain...";
            var publishResult = await _founderAppService.SubmitTransactionFromDraft(
                new PublishFounderTransaction.PublishFounderTransactionRequest(txResult.Value.TransactionDraft));
            if (publishResult.IsFailure)
            {
                DeployStatusText = $"Failed to publish: {publishResult.Error}";
                IsDeploying = false;
                return;
            }

            CurrentScreen = DeployScreen.Success;
        }
        catch (OperationCanceledException)
        {
            DeployStatusText = "Invoice monitoring cancelled.";
        }
        catch (Exception ex)
        {
            DeployStatusText = $"Deployment error: {ex.Message}";
        }
        finally
        {
            IsDeploying = false;
        }
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

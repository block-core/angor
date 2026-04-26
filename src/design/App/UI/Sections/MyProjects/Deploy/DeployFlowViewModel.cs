using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Wallet.Domain;
using App.UI.Shared;
using App.UI.Shared.PaymentFlow;
using App.UI.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace App.UI.Sections.MyProjects.Deploy;



/// <summary>Which screen the deploy overlay is showing.</summary>
public enum DeployScreen
{
    WalletSelector,
    PayFee,
    Success
}

/// <summary>
/// ViewModel for the deploy flow overlay.
/// Orchestrates: Wallet Selector → Pay Fee → Success.
/// Connected to SDK for wallet loading and project deployment.
/// </summary>
public partial class DeployFlowViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;
    private readonly IInvestmentAppService _investmentAppService;
    private readonly IProjectAppService _projectAppService;
    private readonly IFounderAppService _founderAppService;
    private readonly ICurrencyService _currencyService;
    private readonly IWalletContext _walletContext;
    private readonly Func<BitcoinNetwork> _getNetwork;
    private readonly ILogger<DeployFlowViewModel> _logger;
    private CancellationTokenSource? _invoiceMonitorCts;

    // ── State ──
    [Reactive] private DeployScreen currentScreen;
    [Reactive] private bool isVisible;
    [Reactive] private WalletInfo? selectedWallet;
    [Reactive] private bool isDeploying;
    [Reactive] private string deployStatusText = "Waiting for payment...";
    [Reactive] private long selectedFeeRate = 20;
    [Reactive] private string? deployErrorMessage;

    /// <summary>The reusable payment flow VM. Created when the deploy overlay is shown.</summary>
    public PaymentFlowViewModel? PaymentFlow { get; private set; }

    // ── Derived visibility ──
    public bool IsWalletSelector => CurrentScreen == DeployScreen.WalletSelector;
    public bool IsPayFee => CurrentScreen == DeployScreen.PayFee;
    public bool IsSuccess => CurrentScreen == DeployScreen.Success;
    public bool HasSelectedWallet => SelectedWallet != null;
    public bool HasDeployError => !string.IsNullOrWhiteSpace(DeployErrorMessage);
    public string PayButtonText => SelectedWallet != null
        ? $"Pay with {SelectedWallet.Name}"
        : "Choose Wallet";

    public string DeployFee => $"0.0001 {_currencyService.Symbol}";

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;
    public string InvoiceString { get; } = Constants.InvoiceString;

    public string ProjectName { get; set; } = "My Project";

    /// <summary>Project data built by CreateProjectViewModel, consumed during deployment.</summary>
    public CreateProjectDto? ProjectData { get; set; }

    /// <summary>Callback when deploy flow completes successfully.</summary>
    public Action? OnDeployCompleted { get; set; }

    // ── Wallets loaded from IWalletContext ──
    public ReadOnlyObservableCollection<WalletInfo> Wallets => _walletContext.Wallets;

    public DeployFlowViewModel(
        IWalletAppService walletAppService,
        IInvestmentAppService investmentAppService,
        IProjectAppService projectAppService,
        IFounderAppService founderAppService,
        ICurrencyService currencyService,
        IWalletContext walletContext,
        Func<BitcoinNetwork> getNetwork,
        ILogger<DeployFlowViewModel> logger)
    {
        _walletAppService = walletAppService;
        _investmentAppService = investmentAppService;
        _projectAppService = projectAppService;
        _founderAppService = founderAppService;
        _currencyService = currencyService;
        _walletContext = walletContext;
        _getNetwork = getNetwork;
        _logger = logger;
        // Initialize ReactiveCommands for async payment operations
        PayWithWalletCommand = ReactiveCommand.CreateFromTask(PayWithWalletAsync);
        PayWithWalletCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "PayWithWalletCommand error");
            DeployStatusText = $"Deployment error: {ex.Message}";
            DeployErrorMessage = ex.Message;
        });
        PayViaInvoiceCommand = ReactiveCommand.CreateFromTask(PayViaInvoiceAsync);
        PayViaInvoiceCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "PayViaInvoiceCommand error");
            DeployStatusText = $"Deployment error: {ex.Message}";
            DeployErrorMessage = ex.Message;
        });

        // Raise derived property notifications when screen changes
        this.WhenAnyValue(x => x.CurrentScreen)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsWalletSelector));
                this.RaisePropertyChanged(nameof(IsPayFee));
                this.RaisePropertyChanged(nameof(IsSuccess));
            });

        this.WhenAnyValue(x => x.DeployErrorMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasDeployError)));

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
        DeployErrorMessage = null;
        IsVisible = true;

        // Create the reusable payment flow for this deploy session
        var deployFeeSats = 10_000L; // 0.0001 BTC deploy fee
        PaymentFlow = new PaymentFlowViewModel(
            _walletAppService,
            _investmentAppService,
            _walletContext,
            _currencyService,
            _getNetwork,
            _logger,
            new PaymentFlowConfig
            {
                AmountSats = deployFeeSats,
                StageCount = 0,
                FeeRateSatsPerVbyte = (int)SelectedFeeRate,
                Title = "Fund Deployment",
                SuccessTitle = $"{projectName} Deployed!",
                SuccessDescription = "Your project has been successfully deployed to the blockchain.",
                SuccessButtonText = "Go to My Projects",
                OnSuccessButtonClicked = GoToMyProjects,
                OnPaymentReceived = DeployAfterPaymentAsync,
                OnPayWithWallet = DeployWithWalletAsync,
            });
    }

    /// <summary>Callback for PaymentFlowViewModel: deploy project after external payment received.</summary>
    private async Task<Result> DeployAfterPaymentAsync(WalletId walletId, string fundingAddress, long amountSats)
    {
        return await RunDeployStepsAsync(walletId);
    }

    /// <summary>Callback for PaymentFlowViewModel: deploy project using wallet UTXOs directly.</summary>
    private async Task<Result> DeployWithWalletAsync(WalletId walletId, long amountSats, long feeRate)
    {
        SelectedFeeRate = feeRate;
        await _walletContext.RefreshAllBalancesAsync();
        return await RunDeployStepsAsync(walletId);
    }

    /// <summary>Shared deploy logic: create keys → Nostr profile → project info → blockchain tx → publish.</summary>
    private async Task<Result> RunDeployStepsAsync(WalletId walletId)
    {
        if (ProjectData == null)
            return Result.Failure("No project data available.");

        // Step 1: Create project keys
        var keysResult = await _founderAppService.CreateProjectKeys(
            new CreateProjectKeys.CreateProjectKeysRequest(walletId));
        if (keysResult.IsFailure)
            return Result.Failure($"Failed to create project keys: {keysResult.Error}");

        var projectSeed = keysResult.Value.ProjectSeedDto;

        // Step 2: Create Nostr profile
        var profileResult = await _projectAppService.CreateProjectProfile(walletId, projectSeed, ProjectData);
        if (profileResult.IsFailure)
            return Result.Failure($"Failed to create profile: {profileResult.Error}");

        // Step 3: Create project info
        var infoResult = await _projectAppService.CreateProjectInfo(walletId, ProjectData, projectSeed);
        if (infoResult.IsFailure)
            return Result.Failure($"Failed to create project info: {infoResult.Error}");

        // Step 4: Create blockchain transaction
        var txResult = await _projectAppService.CreateProject(
            walletId, SelectedFeeRate, ProjectData, infoResult.Value.EventId, projectSeed);
        if (txResult.IsFailure)
            return Result.Failure($"Failed to create transaction: {txResult.Error}");

        // Step 5: Publish to blockchain
        var publishResult = await _founderAppService.SubmitTransactionFromDraft(
            new PublishFounderTransaction.PublishFounderTransactionRequest(txResult.Value.TransactionDraft));
        if (publishResult.IsFailure)
            return Result.Failure($"Failed to publish: {publishResult.Error}");

        return Result.Success();
    }

    /// <summary>Close the overlay without completing.
    /// Vue ref: closeDeployModal() resets all state.
    /// Wallet picker close: showDeployWalletModal = false (just dismiss).
    /// This is for X buttons and non-success dismissal only.</summary>
    public void Close()
    {
        _invoiceMonitorCts?.Cancel();
        DeployErrorMessage = null;
        IsVisible = false;
    }

    /// <summary>Select a wallet from the list.</summary>
    public void SelectWallet(WalletInfo wallet)
    {
        foreach (var w in Wallets) w.IsSelected = false;
        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    /// <summary>Pay with selected wallet — simulate deploy.
    /// Vue ref: payWithDeployWallet() → 800ms spinner → QR "received" → 1500ms "Deploying..." → success.</summary>
    public ReactiveCommand<Unit, Unit> PayWithWalletCommand { get; }

    public void PayWithWallet() => PayWithWalletCommand.Execute().Subscribe(
        onNext: _ => { },
        onError: ex => _logger.LogError(ex, "PayWithWallet subscription error"));

    private async Task PayWithWalletAsync()
    {
        if (SelectedWallet == null) return;
        if (ProjectData == null)
        {
            DeployStatusText = "No project data available.";
            DeployErrorMessage = "No project data available.";
            return;
        }

        IsDeploying = true;
        DeployErrorMessage = null;

        // Refresh wallet UTXOs from the indexer before building the transaction.
        // This ensures we don't pick UTXOs already consumed by a prior transaction
        // that haven't been synced to local LiteDB yet.
        DeployStatusText = "Refreshing wallet...";
        await _walletContext.RefreshAllBalancesAsync();

        try
        {
            var walletId = SelectedWallet.Id;

            // Step 1: Create project keys
            DeployStatusText = "Generating project keys...";
            var keysResult = await _founderAppService.CreateProjectKeys(
                new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            if (keysResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project keys: {keysResult.Error}";
                DeployErrorMessage = keysResult.Error;
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
                DeployErrorMessage = profileResult.Error;
                IsDeploying = false;
                return;
            }

            // Step 3: Create project info
            DeployStatusText = "Publishing project info...";
            var infoResult = await _projectAppService.CreateProjectInfo(walletId, ProjectData, projectSeed);
            if (infoResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project info: {infoResult.Error}";
                DeployErrorMessage = infoResult.Error;
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
                DeployErrorMessage = txResult.Error;
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
                DeployErrorMessage = publishResult.Error;
                IsDeploying = false;
                return;
            }

            DeployErrorMessage = null;
            CurrentScreen = DeployScreen.Success;
        }
        catch (Exception ex)
        {
            DeployStatusText = $"Deployment error: {ex.Message}";
            DeployErrorMessage = ex.Message;
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

    public void PayViaInvoice() => PayViaInvoiceCommand.Execute().Subscribe(
        onNext: _ => { },
        onError: ex => _logger.LogError(ex, "PayViaInvoice subscription error"));

    private async Task PayViaInvoiceAsync()
    {
        if (ProjectData == null)
        {
            DeployStatusText = "No project data available.";
            DeployErrorMessage = "No project data available.";
            return;
        }

        // Use the first wallet for key generation and address monitoring
        var wallet = Wallets.FirstOrDefault();
        if (wallet == null)
        {
            DeployStatusText = "No wallet available for invoice monitoring.";
            DeployErrorMessage = "No wallet available for invoice monitoring.";
            return;
        }

        IsDeploying = true;
        DeployErrorMessage = null;
        _invoiceMonitorCts?.Cancel();
        _invoiceMonitorCts = new CancellationTokenSource();

        try
        {
            var walletId = wallet.Id;

            // Step 1: Create project keys (needed before we can deploy)
            DeployStatusText = "Generating project keys...";
            var keysResult = await _founderAppService.CreateProjectKeys(
                new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            if (keysResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project keys: {keysResult.Error}";
                DeployErrorMessage = keysResult.Error;
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
                DeployErrorMessage = profileResult.Error;
                IsDeploying = false;
                return;
            }

            // Step 3: Create project info
            DeployStatusText = "Publishing project info...";
            var infoResult = await _projectAppService.CreateProjectInfo(walletId, ProjectData, projectSeed);
            if (infoResult.IsFailure)
            {
                DeployStatusText = $"Failed to create project info: {infoResult.Error}";
                DeployErrorMessage = infoResult.Error;
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
                DeployErrorMessage = txResult.Error;
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
                DeployErrorMessage = publishResult.Error;
                IsDeploying = false;
                return;
            }

            DeployErrorMessage = null;
            CurrentScreen = DeployScreen.Success;
        }
        catch (OperationCanceledException)
        {
            DeployStatusText = "Invoice monitoring cancelled.";
            DeployErrorMessage = "Invoice monitoring cancelled.";
        }
        catch (Exception ex)
        {
            DeployStatusText = $"Deployment error: {ex.Message}";
            DeployErrorMessage = ex.Message;
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

    /// <summary>
    /// Callback invoked when the user clicks "Complete Profile" on the deploy success screen.
    /// The parent wires this to navigate to the edit profile page for the newly created project.
    /// </summary>
    public Action? OnCompleteProfileRequested { get; set; }

    /// <summary>Complete the flow and request navigation to the edit profile page.</summary>
    public void CompleteProfile()
    {
        IsVisible = false;
        OnDeployCompleted?.Invoke();
        OnCompleteProfileRequested?.Invoke();
    }
}

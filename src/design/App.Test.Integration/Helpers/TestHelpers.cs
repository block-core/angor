using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Shell;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.MyProjects;
using App.UI.Sections.Portfolio;
using App.UI.Sections.Settings;
using App.UI.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration.Helpers;

/// <summary>
/// High-level test helpers for driving the full Avalonia app in headless mode.
/// All interactions go through the visual tree using AutomationIds, simulating
/// real user behavior and making tests portable to Appium E2E tests.
/// </summary>
public static class TestHelpers
{
    // ═══════════════════════════════════════════════════════════════════
    // Common Timeouts
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Standard timeout for UI controls to appear after navigation or modal actions.
    /// </summary>
    public static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum time to wait for the faucet coins to appear in the wallet balance.
    /// </summary>
    public static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum time to wait for deploy/invest transactions to complete.
    /// </summary>
    public static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Interval between balance refresh polls.
    /// </summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum time to wait for data to appear in the SDK after a transaction.
    /// The indexer may lag behind the blockchain significantly on signet.
    /// </summary>
    public static readonly TimeSpan IndexerLagTimeout = TimeSpan.FromMinutes(5);

    // ═══════════════════════════════════════════════════════════════════
    // Window & Shell
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a headless Window containing the real ShellView (resolved from DI).
    /// The ShellView constructor sets its own DataContext to ShellViewModel from App.Services.
    /// </summary>
    public static Window CreateShellWindow(int width = 1280, int height = 800)
    {
        var shellView = new ShellView();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = shellView,
        };
        window.Show();

        // Force a layout pass so the visual tree is fully built
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>
    /// Gets the ShellViewModel from the ShellView inside the window.
    /// </summary>
    public static ShellViewModel GetShellViewModel(this Window window)
    {
        var shellView = window.FindDescendantOfType<ShellView>()
                        ?? throw new InvalidOperationException("ShellView not found in window");
        return (ShellViewModel)shellView.DataContext!;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Control Waiting & Interaction
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Waits until a control with the given AutomationId appears in the visual tree,
    /// polling at the given interval. Returns null on timeout.
    /// </summary>
    public static async Task<T?> WaitForControl<T>(
        this Visual root,
        string automationId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null) where T : Visual
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            // Process pending UI work
            Dispatcher.UIThread.RunJobs();

            var found = root.FindByAutomationId<T>(automationId);
            if (found != null && found.IsVisible)
                return found;

            await Task.Delay(interval);
        }

        return null;
    }

    /// <summary>
    /// Waits until a condition becomes true, polling at the given interval.
    /// </summary>
    public static async Task<bool> WaitForCondition(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (condition())
                return true;

            await Task.Delay(interval);
        }

        return false;
    }

    /// <summary>
    /// Clicks a button found by AutomationId. Raises the Button.Click routed event.
    /// </summary>
    public static async Task ClickButton(this Visual root, string automationId, TimeSpan? timeout = null)
    {
        var button = await root.WaitForControl<Button>(automationId, timeout)
                     ?? throw new TimeoutException($"Button '{automationId}' not found within timeout");

        // Raise the Click event the same way Avalonia does internally
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Types text into a TextBox found by AutomationId.
    /// </summary>
    public static async Task TypeText(this Visual root, string automationId, string text, TimeSpan? timeout = null)
    {
        var textBox = await root.WaitForControl<TextBox>(automationId, timeout)
                      ?? throw new TimeoutException($"TextBox '{automationId}' not found within timeout");

        textBox.Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Gets the text from a TextBlock found by AutomationId.
    /// </summary>
    public static async Task<string?> GetText(this Visual root, string automationId, TimeSpan? timeout = null)
    {
        var textBlock = await root.WaitForControl<TextBlock>(automationId, timeout);
        return textBlock?.Text;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Navigation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigates to a section by selecting the corresponding NavItem in the ShellViewModel.
    /// </summary>
    public static void NavigateToSection(this Window window, string sectionLabel)
    {
        var vm = window.GetShellViewModel();
        var navItem = vm.NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == sectionLabel)
                      ?? throw new InvalidOperationException($"Nav item '{sectionLabel}' not found");

        vm.SelectedNavItem = navItem;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Navigates to Settings by calling ShellViewModel.NavigateToSettings().
    /// </summary>
    public static void NavigateToSettings(this Window window)
    {
        var vm = window.GetShellViewModel();
        vm.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Modal Helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a modal by showing the given content via ShellViewModel.ShowModal().
    /// </summary>
    public static void ShowModal(this Window window, Control content)
    {
        var vm = window.GetShellViewModel();
        vm.ShowModal(content);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Closes the current modal via ShellViewModel.HideModal().
    /// </summary>
    public static void HideModal(this Window window)
    {
        var vm = window.GetShellViewModel();
        vm.HideModal();
        Dispatcher.UIThread.RunJobs();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Visual Tree Search
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Finds the first WalletCard in the visual tree and returns it.
    /// Useful for finding wallet action buttons that are inside template parts.
    /// </summary>
    public static async Task<Visual?> WaitForWalletCard(
        this Visual root,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            // WalletCard buttons have AutomationIds like "WalletCardBtnSend"
            var btn = root.FindByAutomationId<Button>("WalletCardBtnSend");
            if (btn != null && btn.IsVisible)
                return btn;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return null;
    }

    /// <summary>
    /// Finds a control by x:Name in the visual tree (not AutomationId).
    /// Useful for controls that don't have AutomationIds but have x:Name.
    /// </summary>
    public static T? FindByName<T>(this Visual root, string name) where T : Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(c => c.Name == name);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ViewModel Getters
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the FundsViewModel from the current Funds view in the visual tree.
    /// </summary>
    public static FundsViewModel? GetFundsViewModel(this Window window)
    {
        var fundsView = window.GetVisualDescendants()
            .OfType<FundsView>()
            .FirstOrDefault();
        return fundsView?.DataContext as FundsViewModel;
    }

    /// <summary>
    /// Get the MyProjectsViewModel from the current My Projects view.
    /// </summary>
    public static MyProjectsViewModel? GetMyProjectsViewModel(this Window window)
    {
        var myProjectsView = window.GetVisualDescendants()
            .OfType<MyProjectsView>()
            .FirstOrDefault();
        return myProjectsView?.DataContext as MyProjectsViewModel;
    }

    /// <summary>
    /// Get the FindProjectsViewModel from the current Find Projects view.
    /// </summary>
    public static FindProjectsViewModel? GetFindProjectsViewModel(this Window window)
    {
        var findProjectsView = window.GetVisualDescendants()
            .OfType<FindProjectsView>()
            .FirstOrDefault();
        return findProjectsView?.DataContext as FindProjectsViewModel;
    }

    /// <summary>
    /// Get the FundersViewModel from the current Funders view.
    /// </summary>
    public static FundersViewModel? GetFundersViewModel(this Window window)
    {
        var fundersView = window.GetVisualDescendants()
            .OfType<FundersView>()
            .FirstOrDefault();
        return fundersView?.DataContext as FundersViewModel;
    }

    /// <summary>
    /// Get the PortfolioViewModel from the current Funded view.
    /// </summary>
    public static PortfolioViewModel? GetPortfolioViewModel(this Window window)
    {
        var portfolioView = window.GetVisualDescendants()
            .OfType<PortfolioView>()
            .FirstOrDefault();
        return portfolioView?.DataContext as PortfolioViewModel;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Common Test Setup Flows
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigate to Settings, call ConfirmWipeData() on the ViewModel.
    /// Resets all persisted state for a clean test start.
    /// </summary>
    public static async Task WipeExistingData(this Window window)
    {
        Log("  [Wipe] Navigating to Settings...");
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        var settingsView = window.GetVisualDescendants()
            .OfType<SettingsView>()
            .FirstOrDefault();

        if (settingsView?.DataContext is SettingsViewModel settingsVm)
        {
            Log("  [Wipe] Found SettingsViewModel, calling ConfirmWipeData()...");
            settingsVm.ConfirmWipeData();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
            Log("  [Wipe] Wipe completed.");
        }
        else
        {
            Log("  [Wipe] SettingsView/SettingsViewModel not found — skipping wipe.");
        }
    }

    /// <summary>
    /// Navigate to Settings and enable debug mode via the SettingsViewModel.
    /// This sets both PrototypeSettings (persisted) and INetworkConfiguration (in-memory).
    /// </summary>
    public static async Task EnableDebugMode(this Window window)
    {
        Log("  [DebugMode] Navigating to Settings...");
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        var settingsView = window.GetVisualDescendants()
            .OfType<SettingsView>()
            .FirstOrDefault();

        settingsView.Should().NotBeNull("SettingsView should be available to enable debug mode");
        var settingsVm = settingsView!.DataContext as SettingsViewModel;
        settingsVm.Should().NotBeNull("SettingsViewModel should be available to enable debug mode");

        settingsVm!.IsDebugMode = true;
        Dispatcher.UIThread.RunJobs();
        settingsVm.IsDebugMode.Should().BeTrue("Debug mode should be enabled");
        Log("  [DebugMode] Debug mode enabled via SettingsViewModel.");
    }

    /// <summary>
    /// Open the Create Wallet modal from the Funds section, choose Generate,
    /// click Download Seed (gracefully skipped in headless — no file dialog),
    /// click Continue to create the wallet, and close on success.
    /// Navigates to Funds first.
    /// </summary>
    public static async Task CreateWalletViaGenerate(this Window window)
    {
        window.NavigateToSection("Funds");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        Log("  [Generate] Looking for Add Wallet button...");
        var addWalletBtn = FindAddWalletButton(window);
        addWalletBtn.Should().NotBeNull("should find the Add Wallet button");

        addWalletBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        var choicePanel = await window.WaitForControl<StackPanel>("ChoicePanel", UiTimeout);
        choicePanel.Should().NotBeNull("Choice panel should be visible in create wallet modal");
        Log("  [Generate] ChoicePanel visible. Clicking 'Generate New'...");

        await window.ClickButton("BtnGenerate", UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        var backupPanel = await window.WaitForControl<StackPanel>("BackupPanel", UiTimeout);
        backupPanel.Should().NotBeNull("Backup panel should be visible after clicking Generate New");
        Log("  [Generate] BackupPanel visible. Clicking 'Download Seed'...");

        await window.ClickButton("BtnDownloadSeed", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        Log("  [Generate] Clicking 'Continue' to create wallet...");
        await window.ClickButton("BtnContinueBackup", UiTimeout);

        // ── #1 regression: Continue button should show spinner during wallet creation ──
        Dispatcher.UIThread.RunJobs();
        var continueBtn = window.FindByAutomationId<Button>("BtnContinueBackup");
        var continueSpinner = window.FindByName<Projektanker.Icons.Avalonia.Icon>("ContinueSpinner");
        // The spinner should be visible and button disabled while wallet creation is in progress.
        // If the operation completes instantly (test/headless), the success panel will already be
        // shown, so we only assert if the button still exists and is in the processing state.
        if (continueBtn is { IsEnabled: false })
        {
            continueSpinner.Should().NotBeNull("#1: ContinueSpinner should exist on the Continue button");
            continueSpinner!.IsVisible.Should().BeTrue("#1: ContinueSpinner should be visible during wallet creation");
            Log("  [Generate] ✓ #1 verified: Continue spinner visible and button disabled during creation.");
        }
        else
        {
            Log("  [Generate] #1: Wallet creation completed before spinner check (fast path).");
        }

        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("Success panel should appear after wallet generation");
        Log("  [Generate] Wallet created successfully.");

        await window.ClickButton("BtnCreateWalletDone", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        var shellVm = window.GetShellViewModel();
        shellVm.IsModalOpen.Should().BeFalse("modal should be closed after clicking Done");
        Log("  [Generate] Modal closed. Wallet creation complete.");
    }

    /// <summary>
    /// Request testnet coins via the FundsViewModel's GetTestCoinsAsync and wait for balance.
    /// If the faucet is temporarily out of funds, retries the faucet call with backoff
    /// (every 30s) while also polling for balance in between, up to the total timeout.
    /// Must be called while on the Funds section.
    /// </summary>
    public static async Task FundWalletViaFaucet(this Window window)
    {
        var fundsVm = window.GetFundsViewModel();
        fundsVm.Should().NotBeNull("FundsViewModel should be available for faucet request");

        // Get the wallet ID from the first wallet in SeedGroups
        var walletId = fundsVm!.SeedGroups.FirstOrDefault()?.Wallets?.FirstOrDefault()?.Id.Value;
        walletId.Should().NotBeNullOrEmpty("Should have a wallet to fund");

        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        var faucetRetryInterval = TimeSpan.FromSeconds(30);
        var lastFaucetAttempt = DateTime.MinValue;
        var faucetAttempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            // Check if balance is already non-zero
            if (fundsVm.TotalBalance != "0.0000")
            {
                Log($"  [Faucet] Non-zero balance detected: {fundsVm.TotalBalance} (after {faucetAttempts} faucet attempt(s))");
                return;
            }

            // Time to (re-)request from faucet?
            if (DateTime.UtcNow - lastFaucetAttempt >= faucetRetryInterval)
            {
                faucetAttempts++;
                lastFaucetAttempt = DateTime.UtcNow;
                Log($"  [Faucet] Attempt #{faucetAttempts}: calling GetTestCoinsAsync...");

                (bool success, string? error) = await fundsVm.GetTestCoinsAsync(walletId!);
                Dispatcher.UIThread.RunJobs();

                if (success)
                    Log($"  [Faucet] Faucet request succeeded on attempt #{faucetAttempts}");
                else
                    Log($"  [Faucet] Faucet request failed: {error}. Retrying in {faucetRetryInterval.TotalSeconds}s");
            }

            // Poll balance via refresh
            await window.ClickWalletCardButton("WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        fundsVm.TotalBalance.Should().NotBe("0.0000",
            $"Balance should become non-zero within {FaucetBalanceTimeout.TotalSeconds}s. Faucet was attempted {faucetAttempts} time(s).");
    }

    /// <summary>
    /// Open the create project wizard from MyProjectsView.
    /// Calls the ViewModel method directly since the EmptyState button uses PointerPressed.
    /// Wires the deploy completed callback (normally done by MyProjectsView code-behind).
    /// </summary>
    public static async Task OpenCreateWizard(this Window window, MyProjectsViewModel myProjectsVm)
    {
        myProjectsVm.CreateProjectVm.ResetWizard();
        myProjectsVm.LaunchCreateWizard();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.ShowCreateWizard.Should().BeTrue("Create wizard should be visible");

        // Wire the deploy completed callback (normally done by MyProjectsView.OpenCreateWizard code-behind)
        myProjectsVm.CreateProjectVm.OnProjectDeployed = () =>
        {
            myProjectsVm.OnProjectDeployed(myProjectsVm.CreateProjectVm);
            myProjectsVm.CloseCreateWizard();
        };

        Log("  [Wizard] Create wizard opened and callback wired.");
    }

    /// <summary>
    /// Find the "Add Wallet" button in the EmptyState control.
    /// EmptyState is a templated control — the button is inside its template.
    /// </summary>
    public static Button? FindAddWalletButton(this Window window)
    {
        var buttons = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => b.IsVisible);

        foreach (var btn in buttons)
        {
            // Check direct string content
            if (btn.Content is string text && text.Contains("Add Wallet"))
                return btn;

            // Check StackPanel content with TextBlock
            if (btn.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.Text == "Add Wallet")
                        return btn;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Click a button on the WalletCard by its AutomationId.
    /// WalletCard buttons are inside a ControlTemplate, so they may not be
    /// immediately in the visual tree. This method waits for the button to appear.
    /// </summary>
    public static async Task ClickWalletCardButton(this Window window, string automationId)
    {
        var button = await window.WaitForControl<Button>(automationId, UiTimeout);
        button.Should().NotBeNull($"WalletCard button '{automationId}' should be found");

        Log($"  [Click] Clicking '{automationId}'...");
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
    }

    // ═══════════════════════════════════════════════════════════════════
    // UI Flow Helpers (Recovery, Approval, Claim, Release)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drives the real recovery UI flow for an investment: opens detail,
    /// clicks the recovery button, confirms the modal, confirms the fee popup,
    /// and waits for the recovery action to complete.
    /// </summary>
    public static async Task ClickRecoveryFlowAsync(
        this Window window,
        PortfolioViewModel portfolioVm,
        InvestmentViewModel investment,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        portfolioVm.OpenInvestmentDetail(investment);
        Dispatcher.UIThread.RunJobs();

        var detailOpened = await WaitForCondition(
            () => ReferenceEquals(portfolioVm.SelectedInvestment, investment),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!detailOpened)
            throw new TimeoutException("Portfolio selected investment detail did not open");

        var detailViewVisible = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<InvestmentDetailView>().Any(v => v.IsVisible),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!detailViewVisible)
            throw new TimeoutException("InvestmentDetailView did not appear");

        var recoverButton = window.FindByName<Button>("RecoverFundsButton")
            ?? throw new InvalidOperationException("RecoverFundsButton not found in detail view");
        recoverButton.IsVisible.Should().BeTrue("RecoverFundsButton should be visible before clicking it");

        recoverButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, recoverButton));
        Dispatcher.UIThread.RunJobs();

        var modalOpened = await WaitForCondition(
            () => investment.ShowRecoveryModal || investment.ShowReleaseModal || investment.ShowClaimModal,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!modalOpened)
            throw new TimeoutException("Recovery modal did not open");

        var confirmButton = window.FindByName<Button>("ConfirmRecoveryModal")
            ?? window.FindByName<Button>("ConfirmReleaseModal")
            ?? window.FindByName<Button>("ClaimPenaltyButton")
            ?? throw new InvalidOperationException("No visible recovery confirm button found");

        confirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, confirmButton));
        Dispatcher.UIThread.RunJobs();

        var feeConfirmButton = await window.WaitForControl<Button>("FeeConfirmButton", maxWait)
            ?? throw new TimeoutException("FeeSelectionPopup did not appear");

        feeConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, feeConfirmButton));
        Dispatcher.UIThread.RunJobs();

        var completed = await WaitForCondition(
            () => !investment.IsProcessing && (investment.ShowSuccessModal || !string.IsNullOrEmpty(investment.ErrorMessage)),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMilliseconds(200));
        if (!completed)
            throw new TimeoutException("Recovery UI flow did not complete");

        investment.ErrorMessage.Should().BeNullOrEmpty("Recovery flow should complete without UI error");
        investment.ShowSuccessModal.Should().BeTrue("Recovery success modal should be shown after successful recovery");
    }

    public static async Task ClickApproveSignatureAsync(
        this Window window,
        FundersViewModel fundersVm,
        SignatureRequestViewModel signature,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(15);

        window.NavigateToSection("Funders");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        fundersVm.SetFilter("waiting");
        Dispatcher.UIThread.RunJobs();

        var buttonReady = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<Button>().Any(b =>
                b.IsVisible &&
                b.Name == "ApproveButton" &&
                b.Tag is int tag &&
                tag == signature.Id),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!buttonReady)
            throw new TimeoutException($"ApproveButton for signature {signature.Id} did not appear");

        var approveButton = window.GetVisualDescendants().OfType<Button>().First(b =>
            b.IsVisible &&
            b.Name == "ApproveButton" &&
            b.Tag is int tag &&
            tag == signature.Id);

        approveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, approveButton));
        Dispatcher.UIThread.RunJobs();
    }

    public static async Task ClickInvestmentDetailActionAsync(
        this Window window,
        PortfolioViewModel portfolioVm,
        InvestmentViewModel investment,
        string buttonName,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        portfolioVm.OpenInvestmentDetail(investment);
        Dispatcher.UIThread.RunJobs();

        var detailOpened = await WaitForCondition(
            () => ReferenceEquals(portfolioVm.SelectedInvestment, investment),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!detailOpened)
            throw new TimeoutException("Portfolio selected investment detail did not open");

        var buttonVisible = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<Button>().Any(b => b.IsVisible && b.Name == buttonName),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!buttonVisible)
            throw new TimeoutException($"Investment detail action button '{buttonName}' did not appear");

        var button = window.GetVisualDescendants().OfType<Button>().First(b => b.IsVisible && b.Name == buttonName);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();

        var actionCompleted = await WaitForCondition(
            () => !investment.IsProcessing,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!actionCompleted)
            throw new TimeoutException($"Investment detail action '{buttonName}' did not complete");
    }

    public static async Task ClickManageProjectClaimStageAsync(
        this Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project,
        int stageNumber,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();

        var manageOpened = await WaitForCondition(
            () => myProjectsVm.SelectedManageProject != null,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!manageOpened)
            throw new TimeoutException("Manage project view did not open");

        var claimButtonVisible = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<Button>().Any(b =>
                b.IsVisible &&
                b.Classes.Contains("StageClaimBtn") &&
                b.Tag is int tag &&
                tag == stageNumber),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!claimButtonVisible)
            throw new TimeoutException($"Claim button for stage {stageNumber} did not appear");

        var claimButton = window.GetVisualDescendants().OfType<Button>().First(b =>
            b.IsVisible &&
            b.Classes.Contains("StageClaimBtn") &&
            b.Tag is int tag &&
            tag == stageNumber);
        claimButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, claimButton));
        Dispatcher.UIThread.RunJobs();

        var claimSelectedBtn = await WaitForCondition(
            () => window.FindByName<Button>("ClaimSelectedBtn")?.IsVisible == true,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!claimSelectedBtn)
            throw new TimeoutException("ClaimSelectedBtn did not appear");

        var manageVm = myProjectsVm.SelectedManageProject!;

        var selectionReady = await WaitForCondition(
            () => manageVm.SelectedStage?.AvailableTransactions.Count > 0,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!selectionReady)
            throw new TimeoutException("Claim UTXO selection list did not populate");

        foreach (var tx in manageVm.SelectedStage!.AvailableTransactions)
            tx.IsSelected = true;
        Dispatcher.UIThread.RunJobs();

        var claimSelected = window.FindByName<Button>("ClaimSelectedBtn")!;
        claimSelected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, claimSelected));
        Dispatcher.UIThread.RunJobs();

        var feeConfirmButton = await window.WaitForControl<Button>("FeeConfirmButton", maxWait)
            ?? throw new TimeoutException("FeeSelectionPopup did not appear for claim flow");
        feeConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, feeConfirmButton));
        Dispatcher.UIThread.RunJobs();

        var completed = await WaitForCondition(
            () => !manageVm.IsClaiming && manageVm.ShowSuccessModal,
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMilliseconds(200));
        if (!completed)
            throw new TimeoutException("Claim stage flow did not complete");
    }

    public static async Task<bool> ClickManageProjectReleaseFundsAsync(
        this Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();

        var manageOpened = await WaitForCondition(
            () => myProjectsVm.SelectedManageProject != null,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!manageOpened)
            throw new TimeoutException("Manage project view did not open");

        var manageVm = myProjectsVm.SelectedManageProject!;

        var manageView = window.GetVisualDescendants().OfType<ManageProjectView>().FirstOrDefault(v => v.IsVisible)
            ?? throw new InvalidOperationException("Visible ManageProjectView not found");
        manageView.OpenReleaseFundsModal();
        Dispatcher.UIThread.RunJobs();

        var releaseButtonVisible = await WaitForCondition(
            () => window.FindByName<Button>("ReleaseFundsConfirmBtn")?.IsVisible == true,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!releaseButtonVisible)
            throw new TimeoutException("ReleaseFundsConfirmBtn did not appear");

        var releaseButton = window.FindByName<Button>("ReleaseFundsConfirmBtn")!;
        releaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, releaseButton));
        Dispatcher.UIThread.RunJobs();

        var completed = await WaitForCondition(
            () => !manageVm.IsReleasingFunds,
            maxWait,
            TimeSpan.FromMilliseconds(200));
        if (!completed)
            throw new TimeoutException("Release funds flow did not complete");

        return manageVm.ShowReleaseFundsSuccessModal;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Logging
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Write a timestamped log message to the console.
    /// Visible in test output when running with --logger "console;verbosity=detailed".
    /// </summary>
    public static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

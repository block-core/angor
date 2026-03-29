using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.Composition.Adapters;
using App.Test.Integration.Helpers;
using App.UI.Sections.Funds;
using App.UI.Sections.Settings;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// Full end-to-end integration test that boots the real app in headless mode
/// with the "test-send-receive" profile and exercises the complete wallet lifecycle:
///
///   1. Wipe any existing data (via Settings → Danger Zone → Wipe Data)
///   2. Navigate to Funds → see empty state
///   3. Create a wallet (Generate path — generates random seed, downloads backup, continues)
///   4. Request testnet coins via the Faucet button on the WalletCard
///   5. Wait for balance to appear (poll Refresh)
///   6. Get a receive address (open Receive modal → read address → close)
///   7. Send to self (open Send modal → fill address + amount → send → verify TxId)
///   8. Assert final balance > 0
///
/// This test uses real testnet infrastructure (indexer + faucet API) and requires
/// internet connectivity. It may take 30-120 seconds depending on network conditions.
///
/// All UI interactions use AutomationProperties.AutomationId for control lookup,
/// making this test portable to Appium E2E tests in the future.
/// </summary>
public class SendToSelfTest
{
    /// <summary>
    /// Maximum time to wait for the faucet coins to appear in the wallet balance.
    /// The indexer may take a while to pick up the transaction.
    /// </summary>
    private static readonly TimeSpan FaucetBalanceTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Maximum time to wait for the send transaction to be confirmed (success panel visible).
    /// </summary>
    private static readonly TimeSpan SendTransactionTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Standard timeout for UI controls to appear after navigation or modal actions.
    /// </summary>
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Interval between balance refresh polls.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    [AvaloniaFact]
    public async Task FullSendToSelfFlow()
    {
        using var profileScope = TestProfileScope.For(nameof(SendToSelfTest));
        Log("========== STARTING FullSendToSelfFlow ==========");

        // ──────────────────────────────────────────────────────────────
        // ARRANGE: Boot the full app with ShellView
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 0] Booting app with ShellView...");
        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();
        Log("[STEP 0] App booted. ShellView created, ShellViewModel ready.");

        // ──────────────────────────────────────────────────────────────
        // STEP 1: Wipe any existing data to start clean
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 1] Wiping existing data...");
        await WipeExistingData(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 2: Navigate to Funds — verify empty state
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 2] Navigating to Funds section...");
        window.NavigateToSection("Funds");
        await Task.Delay(500); // let the view load and call LoadWalletsFromSdkAsync
        Dispatcher.UIThread.RunJobs();

        var emptyState = await window.WaitForControl<Panel>("EmptyStatePanel", UiTimeout);
        Log($"[STEP 2] EmptyStatePanel found: {emptyState != null}");
        emptyState.Should().NotBeNull("Funds should show empty state after wipe");

        // ──────────────────────────────────────────────────────────────
        // STEP 3: Create wallet via Generate path
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 3] Creating wallet via Generate path...");
        await CreateWalletViaGenerate(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 4: Wait for wallet card to appear in populated state
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 4] Waiting for WalletCard to appear...");
        var walletCardSendBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        Log($"[STEP 4] WalletCard found: {walletCardSendBtn != null}");
        walletCardSendBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        // ──────────────────────────────────────────────────────────────
        // STEP 5: Request testnet coins via Faucet
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 5] Requesting testnet coins via Faucet...");
        await RequestFaucetCoins(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 6: Wait for balance to become non-zero
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 6] Waiting for balance to become non-zero...");
        await WaitForNonZeroBalance(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 7: Get receive address
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 7] Getting receive address...");
        var receiveAddress = await GetReceiveAddress(window);
        Log($"[STEP 7] Receive address: {receiveAddress}");
        receiveAddress.Should().NotBeNullOrWhiteSpace("should get a valid receive address");

        // ──────────────────────────────────────────────────────────────
        // STEP 8: Send to self
        // ──────────────────────────────────────────────────────────────
        Log($"[STEP 8] Sending 0.00010000 BTC to self at {receiveAddress}...");
        var txId = await SendToSelf(window, receiveAddress!);
        Log($"[STEP 8] Send result TxId: {txId}");
        txId.Should().NotBeNullOrWhiteSpace("should get a valid transaction ID after sending");

        // ──────────────────────────────────────────────────────────────
        // STEP 9: Verify balance is still > 0 after send-to-self
        // The indexer may not reflect the new transaction immediately,
        // so poll-refresh until the balance is non-zero (up to 30s).
        // ──────────────────────────────────────────────────────────────
        Log("[STEP 9] Polling balance until non-zero after send-to-self...");
        var step9Deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var step9Polls = 0;
        while (DateTime.UtcNow < step9Deadline)
        {
            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();

            var vm = GetFundsViewModel(window);
            step9Polls++;
            if (vm != null && vm.TotalBalance != "0.0000")
            {
                Log($"[STEP 9] Final balance: {vm.TotalBalance} (after {step9Polls} polls)");
                break;
            }

            Log($"[STEP 9] Poll #{step9Polls}: balance is '{vm?.TotalBalance ?? "N/A"}', retrying...");
        }

        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();
        Log($"[STEP 9] Final balance: {fundsVm!.TotalBalance}");
        fundsVm!.TotalBalance.Should().NotBe("0.0000", "balance should be > 0 after send-to-self (minus fee)");

        // Cleanup: close window
        window.Close();
        Log("========== FullSendToSelfFlow PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private helper methods for each test step
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Navigate to Settings, click "Wipe Data", confirm, then navigate back to Funds.
    /// If the wipe data button isn't found (e.g., no data exists), it's not an error.
    /// </summary>
    private async Task WipeExistingData(Window window)
    {
        Log("  [Wipe] Navigating to Settings...");
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);

        // Look for the settings view and trigger wipe via the ViewModel directly
        // because the BtnWipeData is deep inside a ScrollViewer and the wipe modal
        // is an inline overlay within SettingsView (not a shell modal).
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
    /// Open the Create Wallet modal from the empty state, choose Generate,
    /// click Download Seed (gracefully skipped in headless — no file dialog),
    /// click Continue to create the wallet, and close on success.
    ///
    /// In headless mode, the SaveFilePickerAsync returns null (NoopStorageProvider),
    /// but _seedDownloaded is still set to true and BtnContinueBackup is enabled.
    /// </summary>
    private async Task CreateWalletViaGenerate(Window window)
    {
        // Click the "Add Wallet" button in the EmptyState control.
        Log("  [Generate] Looking for Add Wallet button...");
        var addWalletBtn = FindAddWalletButton(window);
        addWalletBtn.Should().NotBeNull("should find the Add Wallet button in empty state");
        Log("  [Generate] Found Add Wallet button, clicking...");

        addWalletBtn!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // The CreateWalletModal should now be visible as shell modal content.
        // Step 1: Choice panel should be visible
        var choicePanel = await window.WaitForControl<StackPanel>("ChoicePanel", UiTimeout);
        choicePanel.Should().NotBeNull("Choice panel should be visible in create wallet modal");
        Log("  [Generate] ChoicePanel visible. Clicking 'Generate New'...");

        // Click "Generate New" button
        await window.ClickButton("BtnGenerate", UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        // Step 2b: Backup panel should be visible with generated seed words
        var backupPanel = await window.WaitForControl<StackPanel>("BackupPanel", UiTimeout);
        backupPanel.Should().NotBeNull("Backup panel should be visible after clicking Generate New");
        Log("  [Generate] BackupPanel visible. Seed words generated.");

        // Click "Download Seed" — in headless mode, the file picker returns null
        // but _seedDownloaded is set to true regardless, enabling Continue.
        Log("  [Generate] Clicking 'Download Seed' (headless — file dialog will be skipped)...");
        await window.ClickButton("BtnDownloadSeed", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();
        Log("  [Generate] Download Seed clicked. BtnContinueBackup should now be enabled.");

        // Click "Continue" to create the wallet via SDK
        Log("  [Generate] Clicking 'Continue' to create wallet...");
        await window.ClickButton("BtnContinueBackup", UiTimeout);

        // Wait for the async wallet creation to complete and success panel to show
        Log("  [Generate] Waiting for CreateWalletSuccessPanel...");
        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("Success panel should appear after wallet generation");
        Log("  [Generate] Success panel visible. Wallet created.");

        // Click "Done" to close the modal
        await window.ClickButton("BtnCreateWalletDone", UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Modal should be closed
        var shellVm = window.GetShellViewModel();
        shellVm.IsModalOpen.Should().BeFalse("modal should be closed after clicking Done");
        Log("  [Generate] Modal closed. Wallet creation complete.");
    }

    /// <summary>
    /// Click the Faucet button on the WalletCard to request testnet coins.
    /// </summary>
    private async Task RequestFaucetCoins(Window window)
    {
        Log("  [Faucet] Clicking WalletCardBtnFaucet...");
        await ClickWalletCardButton(window, "WalletCardBtnFaucet");

        // The faucet request is async — wait for it to complete.
        // The FundsView shows a toast notification on success/failure.
        // We just wait a bit for the HTTP request to go through.
        Log("  [Faucet] Waiting 5s for faucet HTTP request to complete...");
        await Task.Delay(5000);
        Dispatcher.UIThread.RunJobs();
        Log("  [Faucet] Faucet request should be complete.");
    }

    /// <summary>
    /// Poll the Refresh button until the wallet balance becomes non-zero,
    /// or timeout after FaucetBalanceTimeout.
    /// </summary>
    private async Task WaitForNonZeroBalance(Window window)
    {
        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;
        var pollCount = 0;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            var fundsVm = GetFundsViewModel(window);
            if (fundsVm != null && fundsVm.TotalBalance != "0.0000")
            {
                Log($"  [Balance] Non-zero balance detected: {fundsVm.TotalBalance} (after {pollCount} polls)");
                return;
            }

            pollCount++;
            Log($"  [Balance] Poll #{pollCount}: balance is '{fundsVm?.TotalBalance ?? "N/A"}', refreshing...");

            // Click refresh to poll the indexer
            await ClickWalletCardButton(window, "WalletCardBtnRefresh");
            await Task.Delay(PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        // If we got here, balance never appeared — fail with a message
        var finalVm = GetFundsViewModel(window);
        finalVm.Should().NotBeNull();
        finalVm!.TotalBalance.Should().NotBe("0.0000",
            $"Balance should become non-zero within {FaucetBalanceTimeout.TotalSeconds}s after faucet request. " +
            "The indexer may be slow or the faucet may have failed.");
    }

    /// <summary>
    /// Open the Receive modal from the WalletCard, wait for the address to load
    /// asynchronously (it starts as "Loading..." and updates when the SDK call completes),
    /// read the address, and close.
    /// </summary>
    private async Task<string?> GetReceiveAddress(Window window)
    {
        // Click the Receive button on the WalletCard — this triggers
        // FundsView.OpenReceiveModal() which creates ReceiveFundsModal,
        // calls SetWallet() (which fires LoadReceiveAddressAsync as fire-and-forget),
        // and shows it via ShellViewModel.ShowModal().
        Log("  [Receive] Clicking WalletCardBtnReceive...");
        await ClickWalletCardButton(window, "WalletCardBtnReceive");

        // The ReceiveAddressText TextBlock starts with "Loading..." and is updated
        // asynchronously when GetReceiveAddressAsync completes (involves wallet decryption
        // + HTTP calls to the indexer). We must poll-wait for it to change.
        var addressTimeout = TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow + addressTimeout;
        string? addressText = null;

        Log("  [Receive] Waiting for address to load (polling ReceiveAddressText)...");
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            var textBlock = window.FindByAutomationId<TextBlock>("ReceiveAddressText");
            if (textBlock != null && !string.IsNullOrEmpty(textBlock.Text)
                                  && textBlock.Text != "Loading...")
            {
                addressText = textBlock.Text;
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Log($"  [Receive] Address loaded: {addressText ?? "(null)"}");
        addressText.Should().NotBeNull(
            "Receive address should load within {0}s (should change from 'Loading...' to a tb1... address)",
            addressTimeout.TotalSeconds);

        // Close the modal via the Done button (same as a real user would)
        Log("  [Receive] Closing receive modal...");
        await window.ClickButton("BtnReceiveDone", UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        return addressText;
    }

    /// <summary>
    /// Open the Send modal, fill in the address and a small amount, send, and verify success.
    /// Returns the transaction ID.
    /// </summary>
    private async Task<string?> SendToSelf(Window window, string address)
    {
        // CRITICAL: Ensure the SimplePasswordProvider has the same encryption key
        // that CreateWalletModal used when creating the wallet ("default-key").
        // Without this, SendAmount fails because the SDK cannot decrypt the wallet.
        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");
        Log("  [Send] Set SimplePasswordProvider key to 'default-key'.");

        Log("  [Send] Clicking WalletCardBtnSend...");
        await ClickWalletCardButton(window, "WalletCardBtnSend");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Verify the send form is visible
        var sendForm = await window.WaitForControl<Panel>("SendFormPanel", UiTimeout);
        sendForm.Should().NotBeNull("Send form should be visible after clicking Send");
        Log("  [Send] SendFormPanel visible.");

        // Fill in the address
        Log($"  [Send] Typing address: {address}");
        await window.TypeText("SendAddressInput", address, UiTimeout);

        // Fill in the amount (send a small amount: 0.00010000 BTC = 10000 sats)
        Log("  [Send] Typing amount: 0.00010000");
        await window.TypeText("SendAmountInput", "0.00010000", UiTimeout);

        // Click Send button (fee defaults to Medium = 20 sat/vB)
        Log("  [Send] Clicking BtnSendConfirm...");
        await window.ClickButton("BtnSendConfirm", UiTimeout);

        // Wait for success panel to appear (the send is async, involves SDK + network)
        Log("  [Send] Waiting for SendSuccessPanel...");
        var successPanel = await window.WaitForControl<Panel>("SendSuccessPanel", SendTransactionTimeout);

        // If success panel didn't appear, check for error message to aid debugging
        if (successPanel == null)
        {
            var sendModal = window.GetVisualDescendants()
                .OfType<SendFundsModal>()
                .FirstOrDefault();

            var amountError = sendModal?.FindByName<TextBlock>("AmountError");
            var errorText = amountError is { IsVisible: true } ? amountError.Text : "(no error visible)";
            Log($"  [Send] ERROR: SendSuccessPanel not found. Error shown: {errorText}");
            successPanel.Should().NotBeNull(
                $"Send success panel should appear after transaction. Error shown: {errorText}");
        }

        Log("  [Send] SendSuccessPanel visible. Reading TxId...");

        // Read the transaction ID
        var txId = await window.GetText("SummaryTxid", UiTimeout);
        Log($"  [Send] TxId: {txId}");

        // Click Done to close the send modal
        Log("  [Send] Clicking BtnSendDone...");
        await window.ClickButton("BtnSendDone", UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        return txId;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Utility methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Find the "Add Wallet" button in the EmptyState control.
    /// EmptyState is a templated control — the button is inside its template
    /// and has a StackPanel with "Add Wallet" text.
    /// </summary>
    private Button? FindAddWalletButton(Window window)
    {
        // Find all visible buttons and look for one containing "Add Wallet" text
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
    ///
    /// IMPORTANT: WalletCard buttons have both Name (e.g. "BtnSend") used by the
    /// FundsView click handler routing, and AutomationId (e.g. "WalletCardBtnSend")
    /// used by tests. The click event bubbles up with the correct Source button,
    /// so the Name-based routing in FundsView.OnButtonClick works correctly.
    /// </summary>
    private async Task ClickWalletCardButton(Window window, string automationId)
    {
        var button = await window.WaitForControl<Button>(automationId, UiTimeout);
        button.Should().NotBeNull($"WalletCard button '{automationId}' should be found");

        Log($"  [Click] Clicking '{automationId}'...");
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Get the FundsViewModel from the current Funds view in the visual tree.
    /// </summary>
    private FundsViewModel? GetFundsViewModel(Window window)
    {
        var fundsView = window.GetVisualDescendants()
            .OfType<FundsView>()
            .FirstOrDefault();

        return fundsView?.DataContext as FundsViewModel;
    }

    /// <summary>
    /// Write a timestamped log message to the console.
    /// Visible in test output when running with --logger "console;verbosity=detailed".
    /// </summary>
    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

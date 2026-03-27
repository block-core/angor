using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
///   3. Create a wallet (Import path with known BIP-39 seed words)
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
    /// Known BIP-39 test seed words for deterministic wallet import.
    /// These are standard test words — never use for real funds.
    /// </summary>
    private const string TestSeedWords =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

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
        // ──────────────────────────────────────────────────────────────
        // ARRANGE: Boot the full app with ShellView
        // ──────────────────────────────────────────────────────────────
        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();

        // ──────────────────────────────────────────────────────────────
        // STEP 1: Wipe any existing data to start clean
        // ──────────────────────────────────────────────────────────────
        await WipeExistingData(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 2: Navigate to Funds — verify empty state
        // ──────────────────────────────────────────────────────────────
        window.NavigateToSection("Funds");
        await Task.Delay(500); // let the view load and call LoadWalletsFromSdkAsync
        Dispatcher.UIThread.RunJobs();

        var emptyState = await window.WaitForControl<Panel>("EmptyStatePanel", UiTimeout);
        emptyState.Should().NotBeNull("Funds should show empty state after wipe");

        // ──────────────────────────────────────────────────────────────
        // STEP 3: Create wallet via Import path
        // ──────────────────────────────────────────────────────────────
        await CreateWalletViaImport(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 4: Wait for wallet card to appear in populated state
        // ──────────────────────────────────────────────────────────────
        var walletCardSendBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        walletCardSendBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        // ──────────────────────────────────────────────────────────────
        // STEP 5: Request testnet coins via Faucet
        // ──────────────────────────────────────────────────────────────
        await RequestFaucetCoins(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 6: Wait for balance to become non-zero
        // ──────────────────────────────────────────────────────────────
        await WaitForNonZeroBalance(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 7: Get receive address
        // ──────────────────────────────────────────────────────────────
        var receiveAddress = await GetReceiveAddress(window);
        receiveAddress.Should().NotBeNullOrWhiteSpace("should get a valid receive address");

        // ──────────────────────────────────────────────────────────────
        // STEP 8: Send to self
        // ──────────────────────────────────────────────────────────────
        var txId = await SendToSelf(window, receiveAddress!);
        txId.Should().NotBeNullOrWhiteSpace("should get a valid transaction ID after sending");

        // ──────────────────────────────────────────────────────────────
        // STEP 9: Verify balance is still > 0 after send-to-self
        // ──────────────────────────────────────────────────────────────
        // Refresh and check balance (send-to-self should preserve funds minus fee)
        await ClickWalletCardButton(window, "WalletCardBtnRefresh");
        await Task.Delay(3000);
        Dispatcher.UIThread.RunJobs();

        var fundsVm = GetFundsViewModel(window);
        fundsVm.Should().NotBeNull();
        fundsVm!.TotalBalance.Should().NotBe("0.0000", "balance should be > 0 after send-to-self (minus fee)");

        // Cleanup: close window
        window.Close();
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
            // Call wipe directly — this is equivalent to clicking BtnWipeData + BtnConfirmWipeData
            settingsVm.ConfirmWipeData();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(500);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Open the Create Wallet modal from the empty state, choose Import,
    /// enter test seed words, submit, and close on success.
    /// </summary>
    private async Task CreateWalletViaImport(Window window)
    {
        // Click the "Add Wallet" button in the EmptyState control.
        // EmptyState uses a Button with content containing "Add Wallet" text.
        // The FundsView.OnButtonClick handler detects this via IsAddWalletButton().
        // We need to find the EmptyState's button and click it.
        var addWalletBtn = FindAddWalletButton(window);
        addWalletBtn.Should().NotBeNull("should find the Add Wallet button in empty state");

        addWalletBtn!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, addWalletBtn));
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // The CreateWalletModal should now be visible as shell modal content.
        // Step 1: Choice panel should be visible
        var choicePanel = await window.WaitForControl<StackPanel>("ChoicePanel", UiTimeout);
        choicePanel.Should().NotBeNull("Choice panel should be visible in create wallet modal");

        // Click "Import" button
        await window.ClickButton("BtnImport", UiTimeout);
        await Task.Delay(200);
        Dispatcher.UIThread.RunJobs();

        // Step 2a: Import panel should be visible
        var importPanel = await window.WaitForControl<StackPanel>("ImportPanel", UiTimeout);
        importPanel.Should().NotBeNull("Import panel should be visible after clicking Import");

        // Type seed words into the SeedPhraseInput TextBox
        await window.TypeText("SeedPhraseInput", TestSeedWords, UiTimeout);

        // Click "Import Wallet" submit button
        await window.ClickButton("BtnSubmitImport", UiTimeout);

        // Wait for the async wallet import to complete and success panel to show
        var successPanel = await window.WaitForControl<StackPanel>("CreateWalletSuccessPanel", TimeSpan.FromSeconds(30));
        successPanel.Should().NotBeNull("Success panel should appear after wallet import");

        // Click "Done" to close the modal
        await window.ClickButton("BtnCreateWalletDone", UiTimeout);
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Modal should be closed
        var shellVm = window.GetShellViewModel();
        shellVm.IsModalOpen.Should().BeFalse("modal should be closed after clicking Done");
    }

    /// <summary>
    /// Click the Faucet button on the WalletCard to request testnet coins.
    /// </summary>
    private async Task RequestFaucetCoins(Window window)
    {
        await ClickWalletCardButton(window, "WalletCardBtnFaucet");

        // The faucet request is async — wait for it to complete.
        // The FundsView shows a toast notification on success/failure.
        // We just wait a bit for the HTTP request to go through.
        await Task.Delay(5000);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Poll the Refresh button until the wallet balance becomes non-zero,
    /// or timeout after FaucetBalanceTimeout.
    /// </summary>
    private async Task WaitForNonZeroBalance(Window window)
    {
        var deadline = DateTime.UtcNow + FaucetBalanceTimeout;

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            var fundsVm = GetFundsViewModel(window);
            if (fundsVm != null && fundsVm.TotalBalance != "0.0000" && fundsVm.TotalBalance != "0.0000")
            {
                // Balance is non-zero
                return;
            }

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
        await ClickWalletCardButton(window, "WalletCardBtnReceive");

        // The ReceiveAddressText TextBlock starts with "Loading..." and is updated
        // asynchronously when GetReceiveAddressAsync completes (involves wallet decryption
        // + HTTP calls to the indexer). We must poll-wait for it to change.
        var addressTimeout = TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow + addressTimeout;
        string? addressText = null;

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

        addressText.Should().NotBeNull(
            "Receive address should load within {0}s (should change from 'Loading...' to a tb1... address)",
            addressTimeout.TotalSeconds);

        // Close the modal via the Done button (same as a real user would)
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
        // that CreateWalletModal used when importing the wallet ("default-key").
        // Without this, SendAmount fails because the SDK cannot decrypt the wallet
        // (FrictionlessSensitiveDataProvider tries "DEFAULT", fallback tries "default-encryption-key").
        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");

        await ClickWalletCardButton(window, "WalletCardBtnSend");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Verify the send form is visible
        var sendForm = await window.WaitForControl<Panel>("SendFormPanel", UiTimeout);
        sendForm.Should().NotBeNull("Send form should be visible after clicking Send");

        // Fill in the address
        await window.TypeText("SendAddressInput", address, UiTimeout);

        // Fill in the amount (send a small amount: 0.00010000 BTC = 10000 sats)
        await window.TypeText("SendAmountInput", "0.00010000", UiTimeout);

        // Click Send button (fee defaults to Medium = 20 sat/vB)
        await window.ClickButton("BtnSendConfirm", UiTimeout);

        // Wait for success panel to appear (the send is async, involves SDK + network)
        var successPanel = await window.WaitForControl<Panel>("SendSuccessPanel", SendTransactionTimeout);

        // If success panel didn't appear, check for error message to aid debugging
        if (successPanel == null)
        {
            var sendModal = window.GetVisualDescendants()
                .OfType<SendFundsModal>()
                .FirstOrDefault();

            var amountError = sendModal?.FindByName<TextBlock>("AmountError");
            var errorText = amountError is { IsVisible: true } ? amountError.Text : "(no error visible)";
            successPanel.Should().NotBeNull(
                $"Send success panel should appear after transaction. Error shown: {errorText}");
        }

        // Read the transaction ID
        var txId = await window.GetText("SummaryTxid", UiTimeout);

        // Click Done to close the send modal
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
            if (btn.Content is Avalonia.Controls.StackPanel sp)
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

        button!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, button));
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
}

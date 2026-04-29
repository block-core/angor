using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Angor.Sdk.Common;
using Angor.Shared.Utilities;
using App.Test.Integration.Helpers;
using App.UI.Sections.Funds;
using App.UI.Shared.Services;
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

    [AvaloniaFact]
    public async Task FullSendToSelfFlow()
    {
        using var profileScope = TestProfileScope.For(nameof(SendToSelfTest));
        TestHelpers.Log("========== STARTING FullSendToSelfFlow ==========");

        // ──────────────────────────────────────────────────────────────
        // ARRANGE: Boot the full app with ShellView
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 0] Booting app with ShellView...");
        var window = TestHelpers.CreateShellWindow();
        var shellVm = window.GetShellViewModel();
        TestHelpers.Log("[STEP 0] App booted. ShellView created, ShellViewModel ready.");

        // ──────────────────────────────────────────────────────────────
        // STEP 1: Wipe any existing data to start clean
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 1] Wiping existing data...");
        await window.WipeExistingData();

        // ──────────────────────────────────────────────────────────────
        // STEP 2: Navigate to Funds — verify empty state
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 2] Navigating to Funds section...");
        await window.NavigateToSectionAndVerify("Funds");

        var emptyState = await window.WaitForControl<Panel>("EmptyStatePanel", TestHelpers.UiTimeout);
        TestHelpers.Log($"[STEP 2] EmptyStatePanel found: {emptyState != null}");
        emptyState.Should().NotBeNull("Funds should show empty state after wipe");

        // ──────────────────────────────────────────────────────────────
        // STEP 3: Create wallet via Generate path
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 3] Creating wallet via Generate path...");
        await window.CreateWalletViaGenerate();

        // ──────────────────────────────────────────────────────────────
        // STEP 4: Wait for wallet card to appear in populated state
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 4] Waiting for WalletCard to appear...");
        var walletCardSendBtn = await window.WaitForWalletCard(TimeSpan.FromSeconds(30));
        TestHelpers.Log($"[STEP 4] WalletCard found: {walletCardSendBtn != null}");
        walletCardSendBtn.Should().NotBeNull("WalletCard should appear after wallet creation");

        // ──────────────────────────────────────────────────────────────
        // STEP 5: Request testnet coins via Faucet
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 5] Requesting testnet coins via Faucet...");
        await RequestFaucetCoins(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 6: Wait for balance to become non-zero
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 6] Waiting for balance to become non-zero...");
        await WaitForNonZeroBalance(window);

        // ──────────────────────────────────────────────────────────────
        // STEP 7: Get receive address
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 7] Getting receive address...");
        var receiveAddress = await GetReceiveAddress(window);
        TestHelpers.Log($"[STEP 7] Receive address: {receiveAddress}");
        receiveAddress.Should().NotBeNullOrWhiteSpace("should get a valid receive address");

        // ──────────────────────────────────────────────────────────────
        // STEP 8: Send to self
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log($"[STEP 8] Sending 0.00010000 BTC to self at {receiveAddress}...");
        var txId = await SendToSelf(window, receiveAddress!);
        TestHelpers.Log($"[STEP 8] Send result TxId: {txId}");
        txId.Should().NotBeNullOrWhiteSpace("should get a valid transaction ID after sending");

        // ──────────────────────────────────────────────────────────────
        // STEP 8b: Verify WalletInfo.Balance excludes pending (no double-counting)
        //
        // Right after a send-to-self the wallet should have an unconfirmed
        // incoming transaction. WalletInfo.Balance must show only the confirmed
        // amount (TotalBalanceSats), while PendingBalance shows the unconfirmed
        // amount separately.  Before the fix, Balance used AvailableSats
        // (confirmed + unconfirmed) which double-counted the pending amount
        // because PendingBalance was displayed alongside it.
        // ──────────────────────────────────────────────────────────────
        TestHelpers.Log("[STEP 8b] Verifying WalletInfo pending balance is not double-counted...");
        {
            // Refresh so WalletInfo picks up the new unconfirmed transaction
            await window.ClickWalletCardButton("WalletCardBtnRefresh");
            await Task.Delay(TestHelpers.PollInterval);
            Dispatcher.UIThread.RunJobs();

            var walletContext = global::App.App.Services.GetRequiredService<IWalletContext>();
            var wallet = walletContext.Wallets.FirstOrDefault();
            wallet.Should().NotBeNull("should have at least one wallet after creation");

            TestHelpers.Log($"[STEP 8b] WalletInfo — TotalBalanceSats={wallet!.TotalBalanceSats}, UnconfirmedBalanceSats={wallet.UnconfirmedBalanceSats}, AvailableSats={wallet.AvailableSats}");
            wallet.AvailableSats.Should().Be(wallet.TotalBalanceSats + wallet.UnconfirmedBalanceSats,
                "AvailableSats should equal confirmed + unconfirmed");

            var expectedBalancePrefix = ((double)wallet.TotalBalanceSats.ToUnitBtc()).ToString("F8");
            wallet.Balance.Should().StartWith(expectedBalancePrefix,
                "Balance display should show confirmed sats only (TotalBalanceSats), not AvailableSats which includes unconfirmed");

            if (wallet.UnconfirmedBalanceSats != 0)
            {
                wallet.HasPendingBalance.Should().BeTrue("should have a pending balance after send-to-self");
                wallet.PendingBalance.Should().NotBeEmpty("PendingBalance display should be non-empty when unconfirmed != 0");
                TestHelpers.Log($"[STEP 8b] Pending balance verified: Balance='{wallet.Balance}', PendingBalance='{wallet.PendingBalance}'");
            }
            else
            {
                TestHelpers.Log("[STEP 8b] No unconfirmed balance detected (transaction may have already confirmed). Skipping pending-specific assertions.");
            }
        }

        TestHelpers.Log("[STEP 9] Polling balance until non-zero after send-to-self...");
        var step9Deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        var step9Polls = 0;
        while (DateTime.UtcNow < step9Deadline)
        {
            await window.ClickWalletCardButton("WalletCardBtnRefresh");
            await Task.Delay(TestHelpers.PollInterval);
            Dispatcher.UIThread.RunJobs();

            var uiBalance = await window.GetFundsTotalBalanceFromUi();
            step9Polls++;
            if (uiBalance != null && uiBalance != "0.0000")
            {
                TestHelpers.Log($"[STEP 9] Final balance (from UI): {uiBalance} (after {step9Polls} polls)");
                break;
            }

            TestHelpers.Log($"[STEP 9] Poll #{step9Polls}: balance is '{uiBalance ?? "N/A"}', retrying...");
        }

        var finalBalance = await window.GetFundsTotalBalanceFromUi();
        finalBalance.Should().NotBeNull("FundsTotalBalanceText should be visible in the UI");
        TestHelpers.Log($"[STEP 9] Final balance (from UI): {finalBalance}");
        finalBalance.Should().NotBe("0.0000", "balance should be > 0 after send-to-self (minus fee)");

        // Cleanup: close window
        window.Close();
        TestHelpers.Log("========== FullSendToSelfFlow PASSED ==========");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private helper methods unique to this test
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Click the Faucet button on the WalletCard to request testnet coins.
    /// </summary>
    private async Task RequestFaucetCoins(Window window)
    {
        TestHelpers.Log("  [Faucet] Clicking WalletCardBtnFaucet...");
        await window.ClickWalletCardButton("WalletCardBtnFaucet");

        TestHelpers.Log("  [Faucet] Waiting 5s for faucet HTTP request to complete...");
        await Task.Delay(5000);
        Dispatcher.UIThread.RunJobs();
        TestHelpers.Log("  [Faucet] Faucet request should be complete.");
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

            var fundsVm = window.GetFundsViewModel();
            if (fundsVm != null && fundsVm.TotalBalance != "0.0000")
            {
                TestHelpers.Log($"  [Balance] Non-zero balance detected: {fundsVm.TotalBalance} (after {pollCount} polls)");
                return;
            }

            pollCount++;
            TestHelpers.Log($"  [Balance] Poll #{pollCount}: balance is '{fundsVm?.TotalBalance ?? "N/A"}', refreshing...");

            await window.ClickWalletCardButton("WalletCardBtnRefresh");
            await Task.Delay(TestHelpers.PollInterval);
            Dispatcher.UIThread.RunJobs();
        }

        var finalVm = window.GetFundsViewModel();
        finalVm.Should().NotBeNull();
        finalVm!.TotalBalance.Should().NotBe("0.0000",
            $"Balance should become non-zero within {FaucetBalanceTimeout.TotalSeconds}s after faucet request. " +
            "The indexer may be slow or the faucet may have failed.");
    }

    /// <summary>
    /// Open the Receive modal from the WalletCard, wait for the address to load
    /// asynchronously, read the address, and close.
    /// </summary>
    private async Task<string?> GetReceiveAddress(Window window)
    {
        TestHelpers.Log("  [Receive] Clicking WalletCardBtnReceive...");
        await window.ClickWalletCardButton("WalletCardBtnReceive");

        var addressTimeout = TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow + addressTimeout;
        string? addressText = null;

        TestHelpers.Log("  [Receive] Waiting for address to load (polling ReceiveAddressText)...");
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

        TestHelpers.Log($"  [Receive] Address loaded: {addressText ?? "(null)"}");
        addressText.Should().NotBeNull(
            "Receive address should load within {0}s (should change from 'Loading...' to a tb1... address)",
            addressTimeout.TotalSeconds);

        TestHelpers.Log("  [Receive] Closing receive modal...");
        await window.ClickButton("BtnReceiveDone", TestHelpers.UiTimeout);
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
        TestHelpers.Log("  [Send] Clicking WalletCardBtnSend...");
        await window.ClickWalletCardButton("WalletCardBtnSend");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var sendForm = await window.WaitForControl<Panel>("SendFormPanel", TestHelpers.UiTimeout);
        sendForm.Should().NotBeNull("Send form should be visible after clicking Send");
        TestHelpers.Log("  [Send] SendFormPanel visible.");

        TestHelpers.Log($"  [Send] Typing address: {address}");
        await window.TypeText("SendAddressInput", address, TestHelpers.UiTimeout);

        TestHelpers.Log("  [Send] Typing amount: 0.00010000");
        await window.TypeText("SendAmountInput", "0.00010000", TestHelpers.UiTimeout);

        TestHelpers.Log("  [Send] Clicking BtnSendConfirm...");
        await window.ClickButton("BtnSendConfirm", TestHelpers.UiTimeout);

        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        TestHelpers.Log("  [Send] Confirming fee selection (Standard 20 sat/vB)...");
        await window.ClickButton("FeeConfirmButton", TimeSpan.FromSeconds(30));

        TestHelpers.Log("  [Send] Waiting for SendSuccessPanel...");
        var successPanel = await window.WaitForControl<Panel>("SendSuccessPanel", SendTransactionTimeout);

        if (successPanel == null)
        {
            var sendModal = window.GetVisualDescendants()
                .OfType<SendFundsModal>()
                .FirstOrDefault();

            var amountError = sendModal?.FindByName<TextBlock>("AmountError");
            var errorText = amountError is { IsVisible: true } ? amountError.Text : "(no error visible)";
            TestHelpers.Log($"  [Send] ERROR: SendSuccessPanel not found. Error shown: {errorText}");
            successPanel.Should().NotBeNull(
                $"Send success panel should appear after transaction. Error shown: {errorText}");
        }

        TestHelpers.Log("  [Send] SendSuccessPanel visible. Reading TxId...");

        var txId = await window.GetText("SummaryTxid", TestHelpers.UiTimeout);
        TestHelpers.Log($"  [Send] TxId: {txId}");

        TestHelpers.Log("  [Send] Clicking BtnSendDone...");
        await window.ClickButton("BtnSendDone", TestHelpers.UiTimeout);
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        return txId;
    }
}

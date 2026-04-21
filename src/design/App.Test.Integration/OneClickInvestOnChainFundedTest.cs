using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Shared.Services;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// End-to-end headless integration test for the 1-click invest on-chain flow
/// with real faucet funding. No wallet pre-funding — the faucet pays the
/// invoice address directly, simulating an external payer scanning the QR code.
///
/// Flow:
///   Navigate to project → set amount → Submit → "Pay invoice instead" →
///   on-chain address generated → faucet sends funds → payment detected →
///   BuildInvestmentDraft → threshold check → publish → Success screen.
///
/// All assertions are against UI controls (AutomationIds) to verify what
/// the user actually sees. This makes the test resilient to ViewModel
/// extraction/refactoring — any broken binding will fail the test.
///
/// Requires real testnet infrastructure (indexer + faucet + Nostr relays).
/// </summary>
public class OneClickInvestOnChainFundedTest
{
    private static readonly TimeSpan InvoicePaymentTimeout = TimeSpan.FromMinutes(5);

    [AvaloniaFact]
    public async Task OnChainInvoice_FaucetPaysAddress_ReachesSuccessScreen()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestOnChainFundedTest));
        Log("========== STARTING 1-click invest on-chain FUNDED test ==========");

        // ── Step 1: Boot app, wipe data (no wallet — the invest flow auto-creates one) ──
        Log("[1] Boot app, wipe data...");
        var window = TestHelpers.CreateShellWindow();
        await window.WipeExistingData();

        // ── Step 2: Navigate to Find Projects and find an open project ──
        Log("[2] Finding an open project...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findVm = window.GetFindProjectsViewModel();
        findVm.Should().NotBeNull("FindProjectsViewModel should be available");
        await WaitForProjects(findVm!);

        var project = findVm!.Projects.FirstOrDefault(p => p.IsOpen);
        project.Should().NotBeNull("at least one open project should be available");
        Log($"[2] Found project: '{project!.ProjectName}' ({project.Stages.Count} stages)");

        // ── Step 3: Open invest page and set amount via UI ──
        Log("[3] Opening invest page, setting amount...");
        findVm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = findVm.InvestPageViewModel;
        investVm.Should().NotBeNull("InvestPageViewModel should be created via factory");
        investVm!.CurrentScreen.Should().Be(InvestScreen.InvestForm);

        // Type amount via AutomationId (real UI interaction)
        await window.TypeText("InvestAmountInput", "0.001", TestHelpers.UiTimeout);
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue("0.001 meets the minimum investment threshold");

        // ── Step 4: Submit → wallet selector → pay invoice instead ──
        Log("[4] Submit → wallet selector → pay invoice instead...");
        // SubmitButton is a Border (PointerPressed), use VM for this action
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        // PayInvoiceInsteadButton is a real Button — click via AutomationId
        await window.ClickButton("InvestPayInvoiceBtn", TestHelpers.UiTimeout);
        investVm.CurrentScreen.Should().Be(InvestScreen.Invoice);

        // ── Step 5: Wait for on-chain address to appear in the UI ──
        Log("[5] Waiting for on-chain address...");
        string? invoiceAddress = null;
        var addressDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < addressDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            // Read address from the actual UI TextBlock
            var addressText = await window.GetText("InvestInvoiceAddress", TimeSpan.FromSeconds(1));
            if (addressText != null && (addressText.StartsWith("tb1") || addressText.StartsWith("bc1")))
            {
                invoiceAddress = addressText;
                break;
            }

            if (investVm.ErrorMessage != null)
                throw new Exception($"Invoice flow errored: {investVm.ErrorMessage}");

            await Task.Delay(500);
        }
        invoiceAddress.Should().NotBeNull("on-chain address should appear in the UI");
        Log($"[5] Address from UI: {invoiceAddress}");

        // Verify payment status shows in the UI
        var statusText = await window.GetText("InvestPaymentStatus", TestHelpers.UiTimeout);
        statusText.Should().Contain("Waiting for payment",
            "status pill should show monitoring is active");

        // ── Step 6: Faucet sends funds to the invoice address ──
        // Send more than the investment amount to cover angor fee + miner fee.
        // Investment: 0.001 BTC = 100,000 sats
        // Angor fee: 1% = 1,000 sats
        // Miner fee estimate: ~20 sat/vB × (252 + stages×43) vB ≈ 8,000-10,000 sats
        // Total needed: ~111,000 sats. Send 0.002 BTC (200,000 sats) for safety margin.
        Log("[6] Faucet sending 0.002 BTC to invoice address...");
        var faucet = global::App.App.Services.GetRequiredService<IFaucetService>();
        var faucetResult = await faucet.RequestCoinsAsync(invoiceAddress!, 0.002m);
        faucetResult.IsSuccess.Should().BeTrue(
            $"faucet should send funds to invoice address. Error: {(faucetResult.IsFailure ? faucetResult.Error : "")}");
        Log("[6] Faucet payment sent.");

        // ── Step 7: Wait for payment detection → build → publish → success ──
        Log("[7] Waiting for payment detection and success...");
        var observedStatuses = new List<string>();
        var successDeadline = DateTime.UtcNow + InvoicePaymentTimeout;
        while (DateTime.UtcNow < successDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            // Read status from the actual UI
            var currentStatus = await window.GetText("InvestPaymentStatus", TimeSpan.FromSeconds(1));
            if (currentStatus != null && !observedStatuses.Contains(currentStatus))
            {
                observedStatuses.Add(currentStatus);
                Log($"[7] Status: '{currentStatus}'");
            }

            // Check for success modal via AutomationId
            var successModal = window.FindByAutomationId<Border>("InvestSuccessModal");
            if (successModal is { IsVisible: true })
            {
                Log("[7] Success modal visible!");
                break;
            }

            if (!investVm.IsProcessing && investVm.ErrorMessage != null)
            {
                Log($"[7] Error: {investVm.ErrorMessage}");
                break;
            }

            await Task.Delay(2000);
        }

        // ── Step 8: Verify success via UI controls ──
        Log("[8] Verifying success screen...");
        investVm.PaymentReceived.Should().BeTrue(
            "faucet payment should have been detected by the address monitor");

        observedStatuses.Should().Contain(
            s => s.Contains("Payment received") || s.Contains("Publishing") || s.Contains("Building"),
            "status should have progressed past 'Waiting for payment'");

        var successModal2 = window.FindByAutomationId<Border>("InvestSuccessModal");
        successModal2.Should().NotBeNull("success modal should exist in visual tree");
        successModal2!.IsVisible.Should().BeTrue(
            $"success modal should be visible. Error: {investVm.ErrorMessage ?? "none"}");

        // Read success title from the actual UI TextBlock
        var successTitle = await window.GetText("InvestSuccessTitle", TestHelpers.UiTimeout);
        successTitle.Should().NotBeNullOrWhiteSpace("success title should be displayed");
        Log($"[8] Success title: '{successTitle}'");

        // ── Step 9: Click "View My Investments" via AutomationId ──
        Log("[9] Clicking 'View My Investments'...");
        await window.ClickButton("InvestViewInvestmentsBtn", TestHelpers.UiTimeout);

        window.Close();
        Log("========== 1-click invest on-chain FUNDED test PASSED ==========");
    }

    private static async Task WaitForProjects(FindProjectsViewModel vm, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (vm.Projects.Count > 0)
                return;
            await Task.Delay(250);
        }

        vm.Projects.Should().NotBeEmpty(
            "projects should load from SDK within timeout — ensure testnet indexer/relays are reachable");
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

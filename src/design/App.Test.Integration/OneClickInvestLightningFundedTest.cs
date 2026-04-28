using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace App.Test.Integration;

/// <summary>
/// End-to-end headless integration test for the 1-click invest Lightning flow
/// with real Lightning payment via ThunderHub.
///
/// Flow:
///   Navigate to project → set amount → Submit → "Pay invoice instead" →
///   switch to Lightning tab → Boltz creates reverse swap → BOLT11 invoice displayed →
///   ThunderHub LND wallet pays the invoice → Boltz claims on-chain →
///   payment detected → BuildInvestmentDraft → threshold check → publish → Success screen.
///
/// All assertions are against UI controls (AutomationIds) to verify what
/// the user actually sees.
///
/// Requires:
///   - Real testnet infrastructure (indexer + Nostr relays)
///   - Boltz testnet API (test.boltz.angor.io or env BOLTZ_API_URL)
///   - ThunderHub LND wallet (env THUNDERHUB_URL)
/// </summary>
public class OneClickInvestLightningFundedTest
{
    private static readonly TimeSpan LightningPaymentTimeout = TimeSpan.FromMinutes(5);
    private readonly ITestOutputHelper _output;

    public OneClickInvestLightningFundedTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string LndPayBaseUrl = "https://thunderhub.thedude.cloud/lnd1-pay";

    [AvaloniaFact]
    public async Task LightningInvoice_ManualPay_ReachesSuccessScreen()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestLightningFundedTest));
        Log("========== STARTING 1-click invest LIGHTNING FUNDED test ==========");

        // ── Step 1: Boot app, wipe data (wallet auto-created by invest flow) ──
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

        // Filter for Fund-type projects only — Invest-type projects always require founder
        // approval regardless of amount, which would block this automated test.
        // Fund-type projects auto-publish when the amount is at or below the penalty threshold.
        var fundProjects = findVm!.Projects.Where(p => p.IsOpen && p.IsFundType).ToList();
        Log($"[2] Found {fundProjects.Count} open Fund-type project(s) out of {findVm.Projects.Count} total");
        foreach (var fp in fundProjects)
        {
            Log($"[2]   - '{fp.ProjectName}' (threshold: {fp.PenaltyThresholdSats?.ToString() ?? "null (always requires approval)"} sats, stages: {fp.Stages.Count})");
        }

        var project = fundProjects.FirstOrDefault(p => p.PenaltyThresholdSats.HasValue);
        project.Should().NotBeNull(
            "at least one open Fund-type project with a penalty threshold should be available — " +
            "Invest-type projects always require founder approval and cannot be used in this test");

        var thresholdSats = project!.PenaltyThresholdSats!.Value;
        var investAmountSats = Math.Min(thresholdSats, 100_000L); // at most 0.001 BTC
        var investAmountBtc = (investAmountSats / 100_000_000m).ToString("0.########");
        Log($"[2] Selected project: '{project.ProjectName}' (threshold: {thresholdSats} sats, investing: {investAmountSats} sats / {investAmountBtc} BTC)");

        // ── Step 3: Open invest page and set amount via UI ──
        Log("[3] Opening invest page, setting amount...");
        findVm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = findVm.InvestPageViewModel;
        investVm.Should().NotBeNull("InvestPageViewModel should be created via factory");
        investVm!.CurrentScreen.Should().Be(InvestScreen.InvestForm);

        await window.TypeText("InvestAmountInput", investAmountBtc, TestHelpers.UiTimeout);
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue($"{investAmountBtc} BTC meets the minimum investment threshold");

        // ── Step 4: Submit → wallet selector → pay invoice instead ──
        Log("[4] Submit → wallet selector → pay invoice instead...");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        await window.ClickButton("InvestPayInvoiceBtn", TestHelpers.UiTimeout);
        investVm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        investVm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "on-chain is the default tab");

        // ── Step 4b: Wait for receive address to be generated ──
        // ShowInvoice() runs GenerateReceiveAddressAsync() which creates the wallet,
        // refreshes balances, and generates the receive address once. Both on-chain and
        // Lightning tabs reuse this same address — no double generation.
        Log("[4b] Waiting for receive address...");
        var initDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < initDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (investVm.OnChainAddress != null)
                break;
            await Task.Delay(250);
        }
        investVm.OnChainAddress.Should().NotBeNullOrEmpty(
            "receive address should be generated before switching to Lightning tab");
        Log($"[4b] Receive address ready: {investVm.OnChainAddress}");

        // ── Step 5: Switch to Lightning tab ──
        Log("[5] Switching to Lightning tab...");
        investVm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        investVm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        investVm.IsLightningTab.Should().BeTrue();

        // ── Step 6: Wait for BOLT11 invoice to appear in the UI ──
        Log("[6] Waiting for Lightning invoice...");
        var invoiceDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < invoiceDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            // Check the UI TextBlock shows an invoice (may be truncated by TextTrimming)
            var invoiceText = await window.GetText("InvestInvoiceAddress", TimeSpan.FromSeconds(1));
            if (invoiceText != null && invoiceText.StartsWith("ln", StringComparison.OrdinalIgnoreCase)
                                    && !invoiceText.Contains("..."))
                break;

            if (investVm.ErrorMessage != null)
                throw new Exception($"Lightning flow errored: {investVm.ErrorMessage}");

            await Task.Delay(500);
        }

        // Read the full invoice from the VM — the UI TextBlock truncates it via TextTrimming
        var bolt11Invoice = investVm.LightningInvoice;
        bolt11Invoice.Should().NotBeNullOrEmpty("BOLT11 invoice should be generated by Boltz swap");
        bolt11Invoice.Should().StartWith("ln", "BOLT11 invoices start with 'ln' prefix");

        // ── Step 7: Pay the invoice via LND REST API ──
        var macaroon = TestSecrets.Get("LND1_PAY_MACAROON");
        if (string.IsNullOrEmpty(macaroon))
        {
            Log("[7] LND1_PAY_MACAROON not set — logging invoice for manual payment.");
            Log("[7] Set it via: dotnet user-secrets set LND1_PAY_MACAROON <hex>");
            Log($"[7] {bolt11Invoice}");
        }
        else
        {
            Log("[7] Paying invoice via LND REST API...");
            using var lnd = new LndPayClient(LndPayBaseUrl, macaroon);
            var preimage = await lnd.PayInvoiceAsync(bolt11Invoice);
            Log($"[7] Payment succeeded. Preimage: {preimage}");
        }

        // ── Step 8: Wait for payment detection → Boltz claim → build → publish → success ──
        Log("[8] Waiting for payment detection and success...");
        var observedStatuses = new List<string>();
        var reachedSuccess = false;
        var successDeadline = DateTime.UtcNow + LightningPaymentTimeout;
        while (DateTime.UtcNow < successDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            var currentStatus = await window.GetText("InvestPaymentStatus", TimeSpan.FromSeconds(1));
            if (currentStatus != null && !observedStatuses.Contains(currentStatus))
            {
                observedStatuses.Add(currentStatus);
                Log($"[8] Status: '{currentStatus}'");
            }

            // Check for errors — don't require IsProcessing==false since async operations
            // may hang (e.g. HTTP call to indexer) keeping IsProcessing true forever.
            if (investVm.ErrorMessage != null)
            {
                Log($"[8] ERROR: {investVm.ErrorMessage}");
                Assert.Fail($"Investment flow failed at step 8: {investVm.ErrorMessage}");
            }

            // Check success via ViewModel screen state (more reliable than modal visibility)
            if (investVm.CurrentScreen == InvestScreen.Success)
            {
                Log("[8] Success screen reached!");
                reachedSuccess = true;
                break;
            }

            await Task.Delay(2000);
        }

        // ── Step 9: Verify success ──
        Log("[9] Verifying success screen...");
        var lastStatus = observedStatuses.LastOrDefault() ?? "none";
        reachedSuccess.Should().BeTrue(
            $"investment flow should reach success within {LightningPaymentTimeout.TotalMinutes} minutes. " +
            $"Last status: '{lastStatus}', IsProcessing: {investVm.IsProcessing}, Error: {investVm.ErrorMessage ?? "none"}");

        var successTitle = await window.GetText("InvestSuccessTitle", TestHelpers.UiTimeout);
        successTitle.Should().NotBeNullOrWhiteSpace("success title should be displayed");
        Log($"[9] Success title: '{successTitle}'");

        // ── Step 10: Click "View My Investments" via AutomationId ──
        Log("[10] Clicking 'View My Investments'...");
        await window.ClickButton("InvestViewInvestmentsBtn", TestHelpers.UiTimeout);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        // Dispose the DI container to shut down background connections (Nostr relays,
        // Boltz WebSocket) that would otherwise keep the test process alive.
        if (global::App.App.Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (global::App.App.Services is IDisposable disposable)
            disposable.Dispose();

        Log("========== 1-click invest LIGHTNING FUNDED test PASSED ==========");
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

    private void Log(string message) => TestHelpers.Log(_output, message);
}

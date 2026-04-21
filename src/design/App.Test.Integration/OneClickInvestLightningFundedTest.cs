using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;

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
///   - Boltz testnet API (boltz.thedude.cloud or env BOLTZ_API_URL)
///   - ThunderHub LND wallet at thunderhub.thedude.cloud:4005 (LND-2)
/// </summary>
public class OneClickInvestLightningFundedTest
{
    // ThunderHub credentials from environment variables — set before running.
    private static readonly string? ThunderHubUrl = Environment.GetEnvironmentVariable("THUNDERHUB_URL");
    private static readonly string? ThunderHubAccountName = Environment.GetEnvironmentVariable("THUNDERHUB_ACCOUNT");
    private static readonly string? ThunderHubPassword = Environment.GetEnvironmentVariable("THUNDERHUB_PASSWORD");

    private static readonly TimeSpan LightningPaymentTimeout = TimeSpan.FromMinutes(5);

    [AvaloniaFact]
    public async Task LightningInvoice_ThunderHubPays_ReachesSuccessScreen()
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

        await window.TypeText("InvestAmountInput", "0.001", TestHelpers.UiTimeout);
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue("0.001 meets the minimum investment threshold");

        // ── Step 4: Submit → wallet selector → pay invoice instead ──
        Log("[4] Submit → wallet selector → pay invoice instead...");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        await window.ClickButton("InvestPayInvoiceBtn", TestHelpers.UiTimeout);
        investVm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        investVm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "on-chain is the default tab");

        // ── Step 5: Switch to Lightning tab ──
        Log("[5] Switching to Lightning tab...");
        investVm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        investVm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        investVm.IsLightningTab.Should().BeTrue();

        // ── Step 6: Wait for BOLT11 invoice to appear in the UI ──
        Log("[6] Waiting for Lightning invoice...");
        string? bolt11Invoice = null;
        var invoiceDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < invoiceDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            var invoiceText = await window.GetText("InvestInvoiceAddress", TimeSpan.FromSeconds(1));
            if (invoiceText != null && invoiceText.StartsWith("lnbc", StringComparison.OrdinalIgnoreCase))
            {
                bolt11Invoice = invoiceText;
                break;
            }

            if (investVm.ErrorMessage != null)
                throw new Exception($"Lightning flow errored: {investVm.ErrorMessage}");

            await Task.Delay(500);
        }
        bolt11Invoice.Should().NotBeNull("BOLT11 invoice should appear in the UI");
        Log($"[6] Invoice from UI: {bolt11Invoice![..Math.Min(50, bolt11Invoice.Length)]}...");

        // ── Step 7: Pay the invoice via ThunderHub LND-2 ──
        Log("[7] Paying invoice via ThunderHub LND-2...");
        using var thunderHub = new ThunderHubClient(ThunderHubUrl);
        await thunderHub.LoginAsync(ThunderHubAccountName!, ThunderHubPassword!);
        Log("[7] ThunderHub login successful.");

        var paid = await thunderHub.PayInvoiceAsync(bolt11Invoice);
        paid.Should().BeTrue("ThunderHub should successfully pay the BOLT11 invoice");
        Log("[7] Lightning payment sent.");

        // ── Step 8: Wait for payment detection → Boltz claim → build → publish → success ──
        Log("[8] Waiting for payment detection and success...");
        var observedStatuses = new List<string>();
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

            var successModal = window.FindByAutomationId<Border>("InvestSuccessModal");
            if (successModal is { IsVisible: true })
            {
                Log("[8] Success modal visible!");
                break;
            }

            if (!investVm.IsProcessing && investVm.ErrorMessage != null)
            {
                Log($"[8] Error: {investVm.ErrorMessage}");
                break;
            }

            await Task.Delay(2000);
        }

        // ── Step 9: Verify success via UI controls ──
        Log("[9] Verifying success screen...");
        investVm.PaymentReceived.Should().BeTrue(
            "Lightning payment should have been detected after Boltz claim");

        observedStatuses.Should().Contain(
            s => s.Contains("Payment received") || s.Contains("Publishing") || s.Contains("Building"),
            "status should have progressed past 'Waiting for Lightning payment'");

        var successModal2 = window.FindByAutomationId<Border>("InvestSuccessModal");
        successModal2.Should().NotBeNull("success modal should exist in visual tree");
        successModal2!.IsVisible.Should().BeTrue(
            $"success modal should be visible. Error: {investVm.ErrorMessage ?? "none"}");

        var successTitle = await window.GetText("InvestSuccessTitle", TestHelpers.UiTimeout);
        successTitle.Should().NotBeNullOrWhiteSpace("success title should be displayed");
        Log($"[9] Success title: '{successTitle}'");

        // ── Step 10: Click "View My Investments" via AutomationId ──
        Log("[10] Clicking 'View My Investments'...");
        await window.ClickButton("InvestViewInvestmentsBtn", TestHelpers.UiTimeout);

        window.Close();
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

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

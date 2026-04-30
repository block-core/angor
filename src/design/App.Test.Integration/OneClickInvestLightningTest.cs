using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Portfolio;
using App.UI.Shared.PaymentFlow;
using NetworkTab = App.UI.Shared.PaymentFlow.NetworkTab;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// Full headless integration test for the 1-click invest Lightning invoice flow.
/// No mocks — uses the real DI container and SDK wiring.
///
/// Exercises the Lightning-specific state machine:
///   InvestForm → set amount → ShowInvoice → switch to Lightning tab →
///   Boltz swap creation → invoice display → monitoring →
///   (timeout/error expected without testnet) →
///   verify error is from Lightning path (not stale on-chain) →
///   portfolio deduplication after investment completion.
///
/// The InvestPageViewModel is obtained via UI navigation (Find Projects → project detail → invest page)
/// rather than being constructed directly, so the full DI and factory wiring is exercised.
/// </summary>
public class OneClickInvestLightningTest
{
    [AvaloniaFact]
    public async Task LightningInvoiceFlow_FullStateMachine()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestLightningTest));
        Log("========== STARTING 1-click invest LIGHTNING test ==========");

        var window = TestHelpers.CreateShellWindow();

        // ── Navigate to Find Projects and get an open project ──
        Log("[0] Navigating to Find Projects and loading projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findVm = window.GetFindProjectsViewModel();
        findVm.Should().NotBeNull("FindProjectsViewModel should be available");
        await WaitForProjects(findVm!);

        var project = findVm!.Projects.FirstOrDefault(p => p.IsOpen);
        project.Should().NotBeNull("at least one open project should be available for testing");

        // ── Open project detail and invest page via UI navigation ──
        findVm.OpenProjectDetail(project!);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var vm = findVm.InvestPageViewModel;
        vm.Should().NotBeNull("InvestPageViewModel should be created via factory");

        // ── Step 1: Set amount, submit, then enter invoice screen ──
        Log("[1] Set amount, submit, show invoice...");
        vm!.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeTrue();

        vm.Submit();
        Dispatcher.UIThread.RunJobs();

        var pf = vm.PaymentFlow;
        pf.Should().NotBeNull("PaymentFlow should be created after Submit()");

        pf!.ShowInvoice();
        Dispatcher.UIThread.RunJobs();
        pf.CurrentScreen.Should().Be(PaymentFlowScreen.Invoice);
        pf.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "ShowInvoice defaults to on-chain");

        // ── Step 2: Switch to Lightning tab ──
        Log("[2] Switch to Lightning tab...");
        pf.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        pf.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        pf.IsLightningTab.Should().BeTrue();
        pf.IsOnChainTab.Should().BeFalse();
        pf.InvoiceFieldLabel.Should().Be("Lightning Invoice");
        pf.InvoiceTabIcon.Should().Contain("bolt");

        // Lightning flow may have already failed fast if address isn't ready yet
        // (PayViaLightningAsync checks OnChainAddress and returns immediately if null).
        // The key assertion is that the tab switched correctly.

        // ── Step 3: Let Lightning flow run — expect error or invoice ──
        Log("[3] Pumping UI to let Lightning flow complete...");
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(pf.ErrorMessage) ||
                  !string.IsNullOrEmpty(pf.LightningInvoice) ||
                  !pf.IsGeneratingLightningInvoice,
            TimeSpan.FromSeconds(15));

        Log($"    LightningInvoice: '{pf.LightningInvoice}'");
        Log($"    LightningSwapId: '{pf.LightningSwapId}'");
        Log($"    ErrorMessage: '{pf.ErrorMessage}'");
        Log($"    IsGeneratingLightningInvoice: {pf.IsGeneratingLightningInvoice}");

        if (pf.LightningInvoice != null)
        {
            pf.LightningSwapId.Should().NotBeNullOrEmpty("swap ID must be set when invoice is available");
            pf.IsGeneratingLightningInvoice.Should().BeFalse("spinner clears after invoice generation");
            pf.InvoiceString.Should().Be(pf.LightningInvoice,
                "InvoiceString shows the actual BOLT11 invoice once available");
        }
        else if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("Parameter 'key'",
                "raw ArgumentNullException must not leak to user");
            pf.ErrorMessage.Should().NotContain("monitoring has stopped",
                "on-chain monitoring error must not appear on the Lightning tab");
            pf.HasError.Should().BeTrue();
        }

        // ── Step 4: Verify on-chain error does NOT bleed into Lightning tab ──
        Log("[4] Verify no on-chain monitoring error bleeds through...");
        if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("monitoring has stopped",
                "cancelled on-chain monitor must not overwrite Lightning tab error state");
            pf.ErrorMessage.Should().NotContain("monitoring was cancelled");
        }

        // ── Step 5: Switch back to on-chain, then back to Lightning ──
        Log("[5] Tab round-trip: OnChain → Lightning...");
        pf.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();
        // The OnChain flow starts immediately and may set its own error (e.g. "No wallet available").
        // The key check is that the previous Lightning error was cleared — any new error is from OnChain.
        if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("Lightning",
                "previous Lightning error should not persist after switching to OnChain tab");
            pf.ErrorMessage.Should().NotContain("monitoring has stopped",
                "cancelled monitoring error should not bleed through tab switch");
        }
        pf.IsOnChainTab.Should().BeTrue();

        pf.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        // Lightning flow starts async and may fail quickly (e.g. no wallet).
        // The key check is that the previous OnChain error was cleared.
        if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("monitoring has stopped",
                "OnChain monitoring error should not persist after switching to Lightning tab");
        }
        pf.IsLightningTab.Should().BeTrue();

        // ── Step 6: Stub tabs don't crash ──
        Log("[6] Stub tabs (Liquid, Import) don't crash...");
        pf.SelectNetworkTab(NetworkTab.Liquid);
        Dispatcher.UIThread.RunJobs();
        pf.SelectedNetworkTab.Should().Be(NetworkTab.Liquid);
        pf.InvoiceFieldLabel.Should().Be("Liquid Address");
        pf.IsProcessing.Should().BeFalse("stub tabs don't start async work");

        pf.SelectNetworkTab(NetworkTab.Import);
        Dispatcher.UIThread.RunJobs();
        pf.SelectedNetworkTab.Should().Be(NetworkTab.Import);
        pf.InvoiceFieldLabel.Should().Be("Imported Invoice");

        // ── Step 7: Reset clears all payment flow state ──
        Log("[7] Reset clears all Lightning state...");
        pf.Reset();
        Dispatcher.UIThread.RunJobs();

        pf.CurrentScreen.Should().Be(PaymentFlowScreen.WalletSelector);
        pf.IsProcessing.Should().BeFalse();
        pf.ErrorMessage.Should().BeNull();
        pf.OnChainAddress.Should().BeNull();
        pf.LightningInvoice.Should().BeNull();
        pf.LightningSwapId.Should().BeNull();
        pf.IsGeneratingLightningInvoice.Should().BeFalse();
        pf.PaymentReceived.Should().BeFalse();

        window.Close();
        Log("========== 1-click invest LIGHTNING test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task PortfolioDeduplication_AfterInvestment()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestLightningTest) + "_Dedup");
        Log("========== STARTING portfolio deduplication test ==========");

        var window = TestHelpers.CreateShellWindow();

        // ── Navigate to Find Projects to get a real project for deduplication testing ──
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findVm = window.GetFindProjectsViewModel();
        findVm.Should().NotBeNull();
        await WaitForProjects(findVm!);

        var project = findVm!.Projects.First(p => p.IsOpen);

        // Get portfolio VM via DI (it's a singleton — same instance the app uses)
        var portfolioVm = global::App.App.Services.GetRequiredService<PortfolioViewModel>();

        // Simulate adding an investment from the invest flow (optimistic add)
        var initialCount = portfolioVm.Investments.Count;
        portfolioVm.AddInvestmentFromProject(project, "0.00100000");

        portfolioVm.Investments.Count.Should().Be(initialCount + 1,
            "first add should insert a new entry");

        var firstEntry = portfolioVm.Investments[0];
        firstEntry.ProjectIdentifier.Should().Be(project.ProjectId);
        firstEntry.StatusText.Should().Contain("Active",
            "below-threshold Fund investment should be auto-approved as active");

        // Simulate the race: add same project again (as if SDK load returned it)
        portfolioVm.AddInvestmentFromProject(project, "0.00100000");

        portfolioVm.Investments.Count.Should().Be(initialCount + 1,
            "second add of the same project must NOT create a duplicate — " +
            "the deduplication fix should detect the existing entry by ProjectIdentifier");

        window.Close();
        Log("========== Portfolio deduplication test PASSED ==========");
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

    private static async Task PumpUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }
}

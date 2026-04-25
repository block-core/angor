using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;

namespace App.Test.Integration;

/// <summary>
/// Full headless integration test for the 1-click invest on-chain invoice flow.
/// No mocks — uses the real DI container and SDK wiring.
///
/// Exercises the complete state machine:
///   InvestForm → set amount → ShowInvoice → on-chain address generation →
///   monitoring starts → (timeout/error expected without testnet) →
///   tab switching clears on-chain error (bug fix verification) →
///   CloseModal resets everything.
///
/// The InvestPageViewModel is obtained via UI navigation (Find Projects → project detail → invest page)
/// rather than being constructed directly, so the full DI and factory wiring is exercised.
/// </summary>
public class OneClickInvestOnChainTest
{
    [AvaloniaFact]
    public async Task OnChainInvoiceFlow_FullStateMachine()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestOnChainTest));
        Log("========== STARTING 1-click invest ON-CHAIN test ==========");

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

        // ── Step 1: Initial state ──
        Log("[1] Verify initial form state...");
        vm!.CurrentScreen.Should().Be(InvestScreen.InvestForm);
        vm.IsProcessing.Should().BeFalse();
        vm.HasError.Should().BeFalse();
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);

        // ── Step 2: Set investment amount ──
        Log("[2] Set investment amount...");
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeTrue("0.001 meets the minimum investment threshold");
        vm.FormattedAmount.Should().Be("0.00100000");

        // ── Step 3: ShowInvoice transitions to Invoice screen, starts on-chain flow ──
        Log("[3] ShowInvoice — on-chain default...");
        vm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "on-chain is the default tab");
        vm.IsProcessing.Should().BeTrue("monitoring starts synchronously");
        vm.PaymentStatusText.Should().NotBeNullOrEmpty(
            "status text should show progress — exact value depends on wallet state");

        // Derived tab visibility
        vm.IsOnChainTab.Should().BeTrue();
        vm.IsLightningTab.Should().BeFalse();
        vm.InvoiceFieldLabel.Should().Be("On-Chain Address");
        vm.InvoiceTabIcon.Should().Contain("bitcoin");

        // ── Step 4: Let the async on-chain flow run — expect error (no wallet/no testnet) ──
        Log("[4] Pumping UI to let on-chain flow complete...");
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.OnChainAddress != null,
            TimeSpan.FromSeconds(10));

        Log($"    OnChainAddress: '{vm.OnChainAddress}'");
        Log($"    ErrorMessage: '{vm.ErrorMessage}'");
        Log($"    PaymentStatusText: '{vm.PaymentStatusText}'");

        // Without a funded testnet wallet we expect a labeled error
        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("Parameter 'key'",
                "raw ArgumentNullException must not leak — defensive pre-checks should catch it");
            vm.HasError.Should().BeTrue("HasError must reflect the ErrorMessage state");
        }

        // ── Step 5: Switch to Lightning — on-chain error must clear (bug fix) ──
        Log("[5] Switch to Lightning tab — error must clear...");
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        vm.IsLightningTab.Should().BeTrue();
        vm.IsOnChainTab.Should().BeFalse();
        vm.InvoiceFieldLabel.Should().Be("Lightning Invoice");
        vm.InvoiceTabIcon.Should().Contain("bolt");

        // The critical bug fix: on-chain monitoring error must not bleed through.
        // The Lightning path may set its own error (e.g. "No wallet available for Lightning swap")
        // which is fine — what matters is the on-chain monitoring error is gone.
        // Give a moment for any racing cancelled-monitor result to arrive.
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(vm.ErrorMessage) || vm.LightningInvoice != null,
            TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();

        if (vm.ErrorMessage != null)
        {
            vm.ErrorMessage.Should().NotContain("monitoring",
                "cancelled on-chain monitoring errors must not leak into the Lightning tab — " +
                "any error here must come from the Lightning path itself");
        }

        // ── Step 6: Switch back to on-chain — fresh flow starts ──
        Log("[6] Switch back to OnChain tab...");
        vm.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);
        vm.IsOnChainTab.Should().BeTrue();
        // The on-chain flow starts but may complete instantly (no wallet → immediate error),
        // so we only verify the tab switched correctly.

        // ── Step 7: CloseModal resets everything ──
        Log("[7] CloseModal resets all state...");
        vm.CloseModal();
        Dispatcher.UIThread.RunJobs();

        vm.CurrentScreen.Should().Be(InvestScreen.InvestForm);
        vm.IsProcessing.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
        vm.OnChainAddress.Should().BeNull();
        vm.LightningInvoice.Should().BeNull();
        vm.LightningSwapId.Should().BeNull();
        vm.IsGeneratingLightningInvoice.Should().BeFalse();
        vm.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);
        vm.PaymentReceived.Should().BeFalse();

        window.Close();
        Log("========== 1-click invest ON-CHAIN test PASSED ==========");
    }

    [AvaloniaFact]
    public async Task OnChainInvoiceFlow_TabSwitchRaceCondition()
    {
        using var profileScope = TestProfileScope.For(nameof(OneClickInvestOnChainTest) + "_Race");
        Log("========== STARTING tab-switch race condition test ==========");

        var window = TestHelpers.CreateShellWindow();

        // ── Navigate to Find Projects and get an open project ──
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var findVm = window.GetFindProjectsViewModel();
        findVm.Should().NotBeNull();
        await WaitForProjects(findVm!);

        var project = findVm!.Projects.First(p => p.IsOpen);
        findVm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        findVm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var vm = findVm.InvestPageViewModel!;

        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();

        // Start on-chain monitoring
        vm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        // Rapidly switch tabs — simulates a user clicking around
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        vm.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();
        vm.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        // Wait for any cancelled operations to settle
        await PumpUntilAsync(
            () => !vm.IsProcessing || !string.IsNullOrEmpty(vm.ErrorMessage) || vm.LightningInvoice != null,
            TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.RunJobs();

        // After rapid switching, we should be on Lightning with no on-chain error bleeding through
        vm.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        if (vm.ErrorMessage != null)
        {
            // Any error should be from the Lightning path, not stale on-chain monitoring
            vm.ErrorMessage.Should().NotContain("monitoring has stopped",
                "stale on-chain monitoring errors must not leak after tab switch");
            vm.ErrorMessage.Should().NotContain("monitoring was cancelled",
                "cancelled on-chain operations must not surface as user-facing errors");
        }

        window.Close();
        Log("========== Tab-switch race condition test PASSED ==========");
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

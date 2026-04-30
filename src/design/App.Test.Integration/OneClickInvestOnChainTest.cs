using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Shared.PaymentFlow;
using Avalonia.Headless.XUnit;
using NetworkTab = App.UI.Shared.PaymentFlow.NetworkTab;
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

        // ── Step 2: Set investment amount and submit ──
        Log("[2] Set investment amount and submit...");
        vm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        vm.CanSubmit.Should().BeTrue("0.001 meets the minimum investment threshold");
        vm.FormattedAmount.Should().Be("0.00100000");

        vm.Submit();
        Dispatcher.UIThread.RunJobs();
        vm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);

        var pf = vm.PaymentFlow;
        pf.Should().NotBeNull("PaymentFlow should be created after Submit()");

        // ── Step 3: ShowInvoice transitions to Invoice screen, starts on-chain flow ──
        Log("[3] ShowInvoice — on-chain default...");
        pf!.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        pf.CurrentScreen.Should().Be(PaymentFlowScreen.Invoice);
        pf.SelectedNetworkTab.Should().Be(NetworkTab.OnChain, "on-chain is the default tab");
        pf.IsProcessing.Should().BeTrue("monitoring starts synchronously");

        // Derived tab visibility
        pf.IsOnChainTab.Should().BeTrue();
        pf.IsLightningTab.Should().BeFalse();
        pf.InvoiceFieldLabel.Should().Be("On-Chain Address");
        pf.InvoiceTabIcon.Should().Contain("bitcoin");

        // ── Step 4: Let the async on-chain flow run — expect error or address ──
        Log("[4] Pumping UI to let on-chain flow complete...");
        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(pf.ErrorMessage) || pf.OnChainAddress != null,
            TimeSpan.FromSeconds(10));

        Log($"    OnChainAddress: '{pf.OnChainAddress}'");
        Log($"    ErrorMessage: '{pf.ErrorMessage}'");
        Log($"    PaymentStatusText: '{pf.PaymentStatusText}'");

        if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("Parameter 'key'",
                "raw ArgumentNullException must not leak — defensive pre-checks should catch it");
            pf.HasError.Should().BeTrue("HasError must reflect the ErrorMessage state");
        }

        // ── Step 5: Switch to Lightning — on-chain error must clear (bug fix) ──
        Log("[5] Switch to Lightning tab — error must clear...");
        pf.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        pf.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        pf.IsLightningTab.Should().BeTrue();
        pf.IsOnChainTab.Should().BeFalse();
        pf.InvoiceFieldLabel.Should().Be("Lightning Invoice");
        pf.InvoiceTabIcon.Should().Contain("bolt");

        await PumpUntilAsync(
            () => !string.IsNullOrEmpty(pf.ErrorMessage) || pf.LightningInvoice != null,
            TimeSpan.FromSeconds(5));
        Dispatcher.UIThread.RunJobs();

        if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("monitoring",
                "cancelled on-chain monitoring errors must not leak into the Lightning tab — " +
                "any error here must come from the Lightning path itself");
        }

        // ── Step 6: Switch back to on-chain — fresh flow starts ──
        Log("[6] Switch back to OnChain tab...");
        pf.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();

        pf.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);
        pf.IsOnChainTab.Should().BeTrue();

        // ── Step 7: Reset resets everything ──
        Log("[7] Reset clears all payment flow state...");
        pf.Reset();
        Dispatcher.UIThread.RunJobs();

        pf.CurrentScreen.Should().Be(PaymentFlowScreen.WalletSelector);
        pf.IsProcessing.Should().BeFalse();
        pf.ErrorMessage.Should().BeNull();
        pf.OnChainAddress.Should().BeNull();
        pf.LightningInvoice.Should().BeNull();
        pf.LightningSwapId.Should().BeNull();
        pf.IsGeneratingLightningInvoice.Should().BeFalse();
        pf.SelectedNetworkTab.Should().Be(NetworkTab.OnChain);
        pf.PaymentReceived.Should().BeFalse();

        window.Close();
        Dispatcher.UIThread.RunJobs();

        if (global::App.App.Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (global::App.App.Services is IDisposable disposable)
            disposable.Dispose();

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

        vm.Submit();
        Dispatcher.UIThread.RunJobs();

        var pf = vm.PaymentFlow!;

        // Start on-chain monitoring
        pf.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        // Rapidly switch tabs — simulates a user clicking around
        pf.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();
        pf.SelectNetworkTab(NetworkTab.OnChain);
        Dispatcher.UIThread.RunJobs();
        pf.SelectNetworkTab(NetworkTab.Lightning);
        Dispatcher.UIThread.RunJobs();

        // Wait for any cancelled operations to settle
        await PumpUntilAsync(
            () => !pf.IsProcessing || !string.IsNullOrEmpty(pf.ErrorMessage) || pf.LightningInvoice != null,
            TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.RunJobs();

        // After rapid switching, we should be on Lightning with no on-chain error bleeding through
        pf.SelectedNetworkTab.Should().Be(NetworkTab.Lightning);
        if (pf.ErrorMessage != null)
        {
            pf.ErrorMessage.Should().NotContain("monitoring has stopped",
                "stale on-chain monitoring errors must not leak after tab switch");
            pf.ErrorMessage.Should().NotContain("monitoring was cancelled",
                "cancelled on-chain operations must not surface as user-facing errors");
        }

        window.Close();
        Dispatcher.UIThread.RunJobs();

        if (global::App.App.Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (global::App.App.Services is IDisposable disposable)
            disposable.Dispose();

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

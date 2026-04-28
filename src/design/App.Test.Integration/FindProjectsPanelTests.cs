using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.Composition.Adapters;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Shared.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration;

/// <summary>
/// Headless tests for the Find Projects section panel visibility state machine.
/// The section has 3 mutually exclusive panels:
///   - ProjectListPanel: grid of project cards (initial state)
///   - ProjectDetailPanel: single project detail view
///   - InvestPagePanel: invest/fund flow
///
/// Consolidated into flow tests to avoid booting 22 separate app instances.
/// </summary>
public class FindProjectsPanelTests
{
    /// <summary>
    /// Flow 1 (no wallet): Tests panel state transitions, project list loading,
    /// project card fields, statistics, detail view metadata, type terminology,
    /// invest button visibility, HasInvested flag, invest form states (initial,
    /// quick amount, manual amount, submit validation), reload, navigate away/back.
    /// </summary>
    [AvaloniaFact]
    public async Task FindProjectsFlow_NoWallet_PanelStatesAndProjectDisplay()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        TestHelpers.Log("═══ Flow 1: No-wallet panel states and project display ═══");

        // ── Navigate to Find Projects ──
        TestHelpers.Log("[1.1] Navigating to Find Projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // ── Initial panel state ──
        TestHelpers.Log("[1.2] Checking initial panel state...");
        var (listPanel, detailPanel, investPanel) = GetPanels(window);

        listPanel.Should().NotBeNull("ProjectListPanel should exist");
        detailPanel.Should().NotBeNull("ProjectDetailPanel should exist");
        investPanel.Should().NotBeNull("InvestPagePanel should exist");

        listPanel!.IsVisible.Should().BeTrue("ProjectListPanel should be visible in initial state");
        detailPanel!.IsVisible.Should().BeFalse("ProjectDetailPanel should be hidden in initial state");
        investPanel!.IsVisible.Should().BeFalse("InvestPagePanel should be hidden in initial state");

        // ── Wait for projects to load from SDK ──
        TestHelpers.Log("[1.3] Waiting for projects to load from SDK...");
        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull("FindProjectsViewModel should be available");
        await WaitForProjects(vm!);

        vm!.Projects.Count.Should().BeGreaterThan(0, "SDK should return at least one project");
        TestHelpers.Log($"[1.3] {vm.Projects.Count} project(s) loaded");

        // ── ProjectCard controls rendered ──
        TestHelpers.Log("[1.4] Verifying ProjectCard controls...");
        // Wait for IsInitialLoad to flip to false (skeleton → real cards transition)
        var cardDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < cardDeadline && vm.IsInitialLoad)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(100);
        }
        Dispatcher.UIThread.RunJobs();
        var cards = window.GetVisualDescendants().OfType<ProjectCard>().ToList();
        cards.Count.Should().BeGreaterThan(0, "at least one ProjectCard should render");

        // ── Card fields match ViewModel ──
        var firstProject = vm.Projects[0];
        var card = cards.FirstOrDefault(c => c.ProjectName == firstProject.ProjectName);
        card.Should().NotBeNull($"should find a ProjectCard for '{firstProject.ProjectName}'");

        TestHelpers.Log($"[1.4] Checking card fields for '{firstProject.ProjectName}'...");
        card!.ProjectName.Should().NotBeNullOrWhiteSpace("ProjectName should be set on card");
        card.Target.Should().NotBeNullOrWhiteSpace("Target should be set on card");
        card.ProjectType.Should().NotBeNullOrWhiteSpace("ProjectType should be set on card");
        card.Status.Should().NotBeNullOrWhiteSpace("Status should be set on card");
        card.TargetLabel.Should().NotBeNullOrWhiteSpace("TargetLabel should be set on card");
        card.ProjectName.Should().Be(firstProject.ProjectName);
        card.Target.Should().Be(firstProject.Target);
        card.ProjectType.Should().Be(firstProject.ProjectType);

        // ── Statistics ──
        // On a fresh local signet, newly created projects won't have investments yet.
        // Only assert statistics if at least one project actually has them.
        TestHelpers.Log("[1.5] Checking project statistics...");
        var statsDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var statsLoaded = false;
        while (DateTime.UtcNow < statsDeadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (vm.Projects.Any(p => p.InvestorCount > 0 || p.Raised != "0.00000"))
            {
                statsLoaded = true;
                break;
            }
            await Task.Delay(500);
        }

        if (statsLoaded)
        {
            var projectWithStats = vm.Projects.First(p => p.InvestorCount > 0 || p.Raised != "0.00000");
            TestHelpers.Log($"[1.5] Project with stats: '{projectWithStats.ProjectName}', investors={projectWithStats.InvestorCount}, raised={projectWithStats.Raised}");
            projectWithStats.InvestorCount.Should().BeGreaterThanOrEqualTo(0);
            if (projectWithStats.Raised != "0.00000" &&
                double.TryParse(projectWithStats.Target, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var target) && target > 0)
            {
                projectWithStats.Progress.Should().BeGreaterThan(0, "Progress should be > 0 when Raised > 0");
            }
        }
        else
        {
            TestHelpers.Log("[1.5] No projects with statistics found (fresh chain). Skipping stats assertions.");
        }

        // ── Open project detail → panel transition ──
        TestHelpers.Log("[1.6] Opening project detail...");
        vm.OpenProjectDetail(firstProject);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        (listPanel, detailPanel, investPanel) = GetPanels(window);
        listPanel!.IsVisible.Should().BeFalse("ProjectListPanel should be hidden when detail is open");
        detailPanel!.IsVisible.Should().BeTrue("ProjectDetailPanel should be visible");
        investPanel!.IsVisible.Should().BeFalse("InvestPagePanel should be hidden");

        // ── Detail view metadata ──
        TestHelpers.Log("[1.6] Checking detail view metadata...");
        var detailView = window.GetVisualDescendants().OfType<ProjectDetailView>().FirstOrDefault();
        detailView.Should().NotBeNull("ProjectDetailView should be in the visual tree");
        detailView!.DataContext.Should().Be(firstProject, "DataContext should be the selected project");

        firstProject.ProjectName.Should().NotBeNullOrWhiteSpace("ProjectName should be set");
        firstProject.Target.Should().NotBeNullOrWhiteSpace("Target should be set");
        firstProject.StartDate.Should().NotBeNullOrWhiteSpace("StartDate should be set");
        firstProject.EndDate.Should().NotBeNullOrWhiteSpace("EndDate should be set");
        firstProject.PenaltyDays.Should().NotBeNullOrWhiteSpace("PenaltyDays should be set");
        firstProject.Stages.Should().NotBeNull("Stages collection should exist");
        if (firstProject.Stages.Count > 0)
        {
            foreach (var stage in firstProject.Stages)
            {
                stage.Percentage.Should().NotBe("0%", $"Stage {stage.StageNumber} percentage should not be 0% (comment #18)");
                stage.Percentage.Should().MatchRegex(@"^\d+%$", $"Stage {stage.StageNumber} percentage should be a valid percentage");
            }
        }
        firstProject.ProjectId.Should().NotBeNullOrWhiteSpace("ProjectId should be set");
        firstProject.FounderKey.Should().NotBeNull("FounderKey should be initialized");

        // ── Type terminology ──
        TestHelpers.Log("[1.7] Checking type terminology...");
        if (firstProject.ProjectType == "Fund")
        {
            firstProject.ActionButtonText.Should().Contain("Fund");
            firstProject.InvestorLabel.Should().Be("Funders");
            firstProject.TargetLabel.Should().Be("Goal:");
        }
        else if (firstProject.ProjectType == "Invest")
        {
            firstProject.ActionButtonText.Should().Contain("Invest");
            firstProject.InvestorLabel.Should().Be("Investors");
            firstProject.TargetLabel.Should().Be("Target:");
        }

        // ── Invest button visibility for open projects ──
        TestHelpers.Log("[1.8] Checking invest button visibility...");
        var openProject = vm.Projects.FirstOrDefault(p => p.IsOpen);
        if (openProject != null && openProject != firstProject)
        {
            vm.CloseProjectDetail();
            Dispatcher.UIThread.RunJobs();
            vm.OpenProjectDetail(openProject);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            detailView = window.GetVisualDescendants().OfType<ProjectDetailView>().FirstOrDefault();
        }
        if (openProject != null)
        {
            var investBtn = detailView!.FindControl<Border>("InvestButton");
            investBtn.Should().NotBeNull("InvestButton should exist in the detail view");
            investBtn!.IsVisible.Should().BeTrue("InvestButton should be visible for an open project");
        }

        // ── HasInvested flag ──
        TestHelpers.Log("[1.9] Checking HasInvested flag...");
        firstProject.HasInvested.Should().BeFalse("fresh test profile should not have invested");
        firstProject.IsOpenAndNotInvested.Should().Be(firstProject.IsOpen);

        // ── Close detail → return to list ──
        TestHelpers.Log("[1.10] Closing detail, returning to list...");
        vm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        (listPanel, detailPanel, investPanel) = GetPanels(window);
        listPanel!.IsVisible.Should().BeTrue("ProjectListPanel should be visible after closing detail");
        detailPanel!.IsVisible.Should().BeFalse();
        investPanel!.IsVisible.Should().BeFalse();

        // ── Open invest page → panel transition ──
        TestHelpers.Log("[1.11] Opening invest page...");
        var openProj = vm.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(openProj);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        (listPanel, detailPanel, investPanel) = GetPanels(window);
        listPanel!.IsVisible.Should().BeFalse();
        detailPanel!.IsVisible.Should().BeFalse();
        investPanel!.IsVisible.Should().BeTrue("InvestPagePanel should be visible");

        // ── Invest form initial state ──
        TestHelpers.Log("[1.12] Checking invest form initial state...");
        var investVm = vm.InvestPageViewModel;
        investVm.Should().NotBeNull("InvestPageViewModel should be created");
        investVm!.CurrentScreen.Should().Be(InvestScreen.InvestForm);
        investVm.IsInvestForm.Should().BeTrue();
        investVm.IsWalletSelector.Should().BeFalse();
        investVm.IsInvoice.Should().BeFalse();
        investVm.IsSuccess.Should().BeFalse();
        investVm.InvestmentAmount.Should().BeEmpty("amount should start empty");

        // ── Quick amount button ──
        TestHelpers.Log("[1.13] Testing quick amount button...");
        investVm.SelectQuickAmount(0.01);
        Dispatcher.UIThread.RunJobs();
        investVm.InvestmentAmount.Should().Be("0.01");
        investVm.SelectedQuickAmount.Should().Be(0.01);
        investVm.CanSubmit.Should().BeTrue("0.01 is above minimum (0.001)");
        if (investVm.Stages.Count > 0)
        {
            investVm.Stages.Should().AllSatisfy(s =>
                s.Amount.Should().NotBe("0.00000000"));
        }

        // ── Manual amount ──
        TestHelpers.Log("[1.14] Testing manual amount entry...");
        investVm.InvestmentAmount = "0.05";
        Dispatcher.UIThread.RunJobs();
        investVm.FormattedAmount.Should().Be("0.05000000");
        investVm.CanSubmit.Should().BeTrue();
        investVm.TotalAmount.Should().NotBeNullOrWhiteSpace("TotalAmount should be computed");
        double.TryParse(investVm.TotalAmount.Split(' ')[0],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var total).Should().BeTrue("TotalAmount should be parseable");
        total.Should().BeGreaterThan(0.05, "total should include fees");

        // ── Submit validation: below minimum ──
        TestHelpers.Log("[1.15] Testing submit validation...");
        investVm.InvestmentAmount = "0.0001";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeFalse("0.0001 is below minimum");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.InvestForm, "should stay on InvestForm");

        // ── Submit validation: at minimum ──
        investVm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue("0.001 is the minimum");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector, "should advance to WalletSelector");

        // ── Close invest page, return to list ──
        vm.CloseInvestPage();
        vm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        // ── Reload projects ──
        TestHelpers.Log("[1.16] Testing reload projects...");
        var initialCount = vm.Projects.Count;
        var loadTask = vm.LoadProjectsFromSdkAsync();
        Dispatcher.UIThread.RunJobs();
        vm.IsLoading.Should().BeTrue("IsLoading should be true while loading projects (comment #2)");
        await loadTask;
        Dispatcher.UIThread.RunJobs();
        vm.Projects.Count.Should().BeGreaterThan(0, "projects should be populated after reload");
        vm.IsLoading.Should().BeFalse("loading flag should be cleared");

        // ── Navigate away and back ──
        TestHelpers.Log("[1.17] Testing navigate away and back...");
        var firstName = vm.Projects[0].ProjectName;
        window.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);

        window.NavigateToSection("Find Projects");
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vmAfter = GetFindProjectsViewModel(window);
        vmAfter.Should().NotBeNull();
        vmAfter!.Projects.Count.Should().BeGreaterThanOrEqualTo(initialCount);
        vmAfter.SelectedProject.Should().BeNull("detail should be closed after navigation");
        vmAfter.InvestPageViewModel.Should().BeNull("invest page should be closed after navigation");

        window.Close();
        TestHelpers.Log("═══ Flow 1 PASSED ═══");
    }

    /// <summary>
    /// Flow 2 (with wallet): Tests wallet selector, wallet display, wallet selection state,
    /// insufficient balance error, invoice screen toggle, close modal reset.
    /// </summary>
    [AvaloniaFact]
    public async Task FindProjectsFlow_WithWallet_WalletSelectorAndInvoice()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests) + "-Wallet");
        var window = TestHelpers.CreateShellWindow();

        TestHelpers.Log("═══ Flow 2: With-wallet selector and invoice flow ═══");

        // ── Create wallet ──
        TestHelpers.Log("[2.1] Creating wallet...");
        await window.WipeExistingData();
        await window.CreateWalletViaGenerate();

        // ── Navigate to Find Projects and load ──
        TestHelpers.Log("[2.2] Navigating to Find Projects...");
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull();
        await WaitForProjects(vm!);

        var project = vm!.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = vm.InvestPageViewModel!;

        // ── Submit to wallet selector ──
        TestHelpers.Log("[2.3] Submitting to wallet selector...");
        investVm.InvestmentAmount = "0.01";
        Dispatcher.UIThread.RunJobs();
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();

        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);
        investVm.IsWalletSelector.Should().BeTrue();

        // ── Wallet display ──
        TestHelpers.Log("[2.4] Checking wallet display...");
        investVm.Wallets.Should().NotBeEmpty("should have at least one wallet");
        var wallet = investVm.Wallets[0];
        wallet.Name.Should().NotBeNullOrWhiteSpace("wallet should have a name");
        wallet.Balance.Should().NotBeNull("wallet should have a balance string");

        // ── Wallet selection state ──
        TestHelpers.Log("[2.5] Testing wallet selection...");
        investVm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();

        investVm.SelectedWallet.Should().Be(wallet);
        investVm.HasSelectedWallet.Should().BeTrue();
        wallet.IsSelected.Should().BeTrue();
        investVm.PayButtonText.Should().Contain(wallet.Name);

        // ── Insufficient balance error ──
        TestHelpers.Log("[2.6] Testing insufficient balance error...");
        // Reset and try with large amount
        investVm.CloseModal();
        Dispatcher.UIThread.RunJobs();

        vm.CloseInvestPage();
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        investVm = vm.InvestPageViewModel!;
        investVm.InvestmentAmount = "100.0";
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();

        wallet = investVm.Wallets[0];
        investVm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();

        var passwordProvider = global::App.App.Services.GetRequiredService<SimplePasswordProvider>();
        passwordProvider.SetKey("default-key");

        var tcs = new TaskCompletionSource();
        var wasProcessingDuringExecution = false;
        investVm.PayWithWalletCommand.Execute().Subscribe(
            _ => { wasProcessingDuringExecution = wasProcessingDuringExecution || investVm.IsProcessing; },
            ex => tcs.TrySetException(ex),
            () => tcs.TrySetResult());
        // Check IsProcessing is set immediately after command starts (comment #9)
        Dispatcher.UIThread.RunJobs();
        wasProcessingDuringExecution = wasProcessingDuringExecution || investVm.IsProcessing;
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Dispatcher.UIThread.RunJobs();

        wasProcessingDuringExecution.Should().BeTrue("IsProcessing should be true while PayWithWallet executes (comment #9)");
        investVm.IsProcessing.Should().BeFalse("IsProcessing should be false after PayWithWallet completes (comment #9)");

        TestHelpers.Log($"[2.6] Error message: {investVm.ErrorMessage}");
        investVm.ErrorMessage.Should().NotBeNullOrWhiteSpace("should show error for insufficient balance");
        investVm.ErrorMessage.Should().Contain("Insufficient");
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector, "should stay on WalletSelector");

        // ── Reset, reopen invest page for invoice/modal tests ──
        investVm.CloseModal();
        Dispatcher.UIThread.RunJobs();
        vm.CloseInvestPage();
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        investVm = vm.InvestPageViewModel!;
        investVm.InvestmentAmount = "0.01";
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();

        // ── Invoice screen toggle ──
        TestHelpers.Log("[2.7] Testing invoice screen toggle...");
        investVm.ShowInvoice();
        Dispatcher.UIThread.RunJobs();

        investVm.CurrentScreen.Should().Be(InvestScreen.Invoice);
        investVm.IsInvoice.Should().BeTrue();
        investVm.IsWalletSelector.Should().BeFalse();

        // Back to wallet selector
        investVm.BackToWalletSelector();
        Dispatcher.UIThread.RunJobs();

        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector);
        investVm.IsWalletSelector.Should().BeTrue();
        investVm.IsInvoice.Should().BeFalse();

        // ── Close modal reset ──
        TestHelpers.Log("[2.8] Testing close modal reset...");
        wallet = investVm.Wallets[0];
        investVm.SelectWallet(wallet);
        Dispatcher.UIThread.RunJobs();
        investVm.SelectedWallet.Should().NotBeNull("precondition: wallet should be selected");

        investVm.CloseModal();
        Dispatcher.UIThread.RunJobs();

        investVm.CurrentScreen.Should().Be(InvestScreen.InvestForm, "should return to InvestForm");
        investVm.SelectedWallet.Should().BeNull("wallet selection should be cleared");
        investVm.IsProcessing.Should().BeFalse("processing flag should be reset");
        investVm.PaymentReceived.Should().BeFalse("payment flag should be reset");
        investVm.ErrorMessage.Should().BeNull("error message should be cleared");
        investVm.PaymentStatusText.Should().Be("Awaiting payment...", "status text should reset");

        window.Close();
        TestHelpers.Log("═══ Flow 2 PASSED ═══");
    }

    /// <summary>
    /// Flow 3 (negative tests): Closed project behavior, invalid input handling.
    /// </summary>
    [AvaloniaFact]
    public async Task FindProjectsFlow_NegativeTests_ClosedProjectAndInvalidInput()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        TestHelpers.Log("═══ Flow 3: Negative tests ═══");

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull();
        await WaitForProjects(vm!);

        // ── Closed project: invest button hidden ──
        TestHelpers.Log("[3.1] Checking closed project behavior...");
        var closedProject = vm!.Projects.FirstOrDefault(p => !p.IsOpen);
        if (closedProject != null)
        {
            vm.OpenProjectDetail(closedProject);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();

            var detailView = window.GetVisualDescendants().OfType<ProjectDetailView>().FirstOrDefault();
            detailView.Should().NotBeNull();

            var investBtn = detailView!.FindControl<Border>("InvestButton");
            if (investBtn != null)
            {
                investBtn.IsVisible.Should().BeFalse("InvestButton should be hidden for a closed project");
            }

            closedProject.IsOpenAndNotInvested.Should().BeFalse(
                "closed project should have IsOpenAndNotInvested = false");

            vm.CloseProjectDetail();
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Log("[3.1] Closed project invest button hidden — verified");
        }
        else
        {
            TestHelpers.Log("[3.1] No closed projects found on testnet — skipping closed project test");
        }

        // ── Invalid input: empty amount ──
        TestHelpers.Log("[3.2] Testing empty amount submission...");
        var openProject = vm.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(openProject);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = vm.InvestPageViewModel!;
        investVm.InvestmentAmount = "";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeFalse("empty amount should not be submittable");
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.InvestForm, "should stay on form with empty amount");

        // ── Invalid input: negative amount ──
        TestHelpers.Log("[3.3] Testing negative amount...");
        investVm.InvestmentAmount = "-0.01";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeFalse("negative amount should not be submittable");

        // ── Invalid input: non-numeric ──
        TestHelpers.Log("[3.4] Testing non-numeric amount...");
        investVm.InvestmentAmount = "abc";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeFalse("non-numeric amount should not be submittable");

        window.Close();
        TestHelpers.Log("═══ Flow 3 PASSED ═══");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static FindProjectsViewModel? GetFindProjectsViewModel(Window window)
    {
        return window.GetVisualDescendants()
            .OfType<FindProjectsView>()
            .FirstOrDefault()?.DataContext as FindProjectsViewModel;
    }

    private static (Visual? list, Panel? detail, Panel? invest) GetPanels(Window window)
    {
        var view = window.GetVisualDescendants()
            .OfType<FindProjectsView>()
            .FirstOrDefault();

        if (view == null) return (null, null, null);

        var list = view.FindControl<ScrollableView>("ProjectListPanel") as Visual;
        var detail = view.FindControl<Panel>("ProjectDetailPanel");
        var invest = view.FindControl<Panel>("InvestPagePanel");
        return (list, detail, invest);
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
}

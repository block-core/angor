using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.Test.Integration.Helpers;
using App.UI.Sections.FindProjects;
using App.UI.Shared.Controls;

namespace App.Test.Integration;

/// <summary>
/// Headless tests for the Find Projects section panel visibility state machine.
/// The section has 3 mutually exclusive panels:
///   - ProjectListPanel: grid of project cards (initial state)
///   - ProjectDetailPanel: single project detail view
///   - InvestPagePanel: invest/fund flow
///
/// These tests boot the full app with real DI, navigate to Find Projects,
/// and verify panel visibility transitions using the ViewModel API.
/// No wallet or network calls required for panel switching itself,
/// but project loading happens on construction (async, may populate from SDK).
/// </summary>
public class FindProjectsPanelTests
{
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(15);

    [AvaloniaFact]
    public async Task FindProjects_InitialState_ShowsProjectListPanel()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        // Navigate to Find Projects
        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        // Get the FindProjectsView and its panels
        var (listPanel, detailPanel, investPanel) = GetPanels(window);

        listPanel.Should().NotBeNull("ProjectListPanel should exist");
        detailPanel.Should().NotBeNull("ProjectDetailPanel should exist");
        investPanel.Should().NotBeNull("InvestPagePanel should exist");

        listPanel!.IsVisible.Should().BeTrue("ProjectListPanel should be visible in initial state");
        detailPanel!.IsVisible.Should().BeFalse("ProjectDetailPanel should be hidden in initial state");
        investPanel!.IsVisible.Should().BeFalse("InvestPagePanel should be hidden in initial state");

        window.Close();
    }

    [AvaloniaFact]
    public async Task FindProjects_OpenProjectDetail_ShowsDetailPanel()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull("FindProjectsViewModel should be available");

        // Wait for projects to load from SDK
        await WaitForProjects(vm!);

        // Open the first project's detail
        vm!.Projects.Should().NotBeEmpty("should have at least one project loaded from SDK");
        vm.OpenProjectDetail(vm.Projects[0]);
        Dispatcher.UIThread.RunJobs();

        var (listPanel, detailPanel, investPanel) = GetPanels(window);

        listPanel!.IsVisible.Should().BeFalse("ProjectListPanel should be hidden when detail is open");
        detailPanel!.IsVisible.Should().BeTrue("ProjectDetailPanel should be visible when detail is open");
        investPanel!.IsVisible.Should().BeFalse("InvestPagePanel should be hidden when detail is open");

        window.Close();
    }

    [AvaloniaFact]
    public async Task FindProjects_CloseProjectDetail_ReturnsToList()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        // Open then close detail
        vm!.OpenProjectDetail(vm.Projects[0]);
        Dispatcher.UIThread.RunJobs();
        vm.CloseProjectDetail();
        Dispatcher.UIThread.RunJobs();

        var (listPanel, detailPanel, investPanel) = GetPanels(window);

        listPanel!.IsVisible.Should().BeTrue("ProjectListPanel should be visible after closing detail");
        detailPanel!.IsVisible.Should().BeFalse("ProjectDetailPanel should be hidden after closing detail");
        investPanel!.IsVisible.Should().BeFalse("InvestPagePanel should be hidden after closing detail");

        window.Close();
    }

    [AvaloniaFact]
    public async Task FindProjects_OpenInvestPage_ShowsInvestPanel()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        // Open detail then invest page
        vm!.OpenProjectDetail(vm.Projects[0]);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var (listPanel, detailPanel, investPanel) = GetPanels(window);

        listPanel!.IsVisible.Should().BeFalse("ProjectListPanel should be hidden when invest page is open");
        detailPanel!.IsVisible.Should().BeFalse("ProjectDetailPanel should be hidden when invest page is open");
        investPanel!.IsVisible.Should().BeTrue("InvestPagePanel should be visible when invest page is open");

        window.Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Project List Loading & Display
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public async Task ProjectList_LoadsFromSdk_ShowsProjectCards()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull();
        await WaitForProjects(vm!);

        // ViewModel has projects
        vm!.Projects.Count.Should().BeGreaterThan(0, "SDK should return at least one project");

        // Visual tree has ProjectCard controls
        Dispatcher.UIThread.RunJobs();
        var cards = window.GetVisualDescendants().OfType<ProjectCard>().ToList();
        cards.Count.Should().BeGreaterThan(0, "at least one ProjectCard should render in the visual tree");

        window.Close();
    }

    [AvaloniaFact]
    public async Task ProjectList_ShowsCorrectFields_OnProjectCard()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull();
        await WaitForProjects(vm!);
        Dispatcher.UIThread.RunJobs();

        var firstProject = vm!.Projects[0];
        var cards = window.GetVisualDescendants().OfType<ProjectCard>().ToList();
        cards.Should().NotBeEmpty();

        // Find the card that matches the first project
        var card = cards.FirstOrDefault(c => c.ProjectName == firstProject.ProjectName);
        card.Should().NotBeNull($"should find a ProjectCard for '{firstProject.ProjectName}'");

        card!.ProjectName.Should().NotBeNullOrWhiteSpace("ProjectName should be set on card");
        card.Target.Should().NotBeNullOrWhiteSpace("Target should be set on card");
        card.ProjectType.Should().NotBeNullOrWhiteSpace("ProjectType should be set on card");
        card.Status.Should().NotBeNullOrWhiteSpace("Status should be set on card");
        card.TargetLabel.Should().NotBeNullOrWhiteSpace("TargetLabel should be set on card");

        // Verify the card data matches the ViewModel
        card.ProjectName.Should().Be(firstProject.ProjectName);
        card.Target.Should().Be(firstProject.Target);
        card.ProjectType.Should().Be(firstProject.ProjectType);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ProjectList_LoadsStatistics_UpdatesRaisedAndInvestorCount()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        vm.Should().NotBeNull();
        await WaitForProjects(vm!);

        // Statistics load is fire-and-forget after project load.
        // Wait for at least one project to have non-default statistics.
        var statsDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var statsLoaded = false;

        while (DateTime.UtcNow < statsDeadline)
        {
            Dispatcher.UIThread.RunJobs();

            // Check if any project has statistics updated (InvestorCount > 0 or Raised != "0.00000")
            if (vm!.Projects.Any(p => p.InvestorCount > 0 || p.Raised != "0.00000"))
            {
                statsLoaded = true;
                break;
            }

            await Task.Delay(500);
        }

        statsLoaded.Should().BeTrue(
            "at least one project should have statistics (InvestorCount > 0 or Raised != '0.00000') — " +
            "this requires testnet projects with existing investments");

        // Verify the project with stats has consistent data
        var projectWithStats = vm!.Projects.First(p => p.InvestorCount > 0 || p.Raised != "0.00000");
        projectWithStats.InvestorCount.Should().BeGreaterThanOrEqualTo(0);

        if (projectWithStats.Raised != "0.00000" &&
            double.TryParse(projectWithStats.Target, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var target) && target > 0)
        {
            projectWithStats.Progress.Should().BeGreaterThan(0, "Progress should be > 0 when Raised > 0");
        }

        window.Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Project Detail View
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public async Task ProjectDetail_DisplaysProjectMetadata()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        var project = vm!.Projects[0];
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // Verify the detail view exists in the visual tree with correct DataContext
        var detailView = window.GetVisualDescendants()
            .OfType<ProjectDetailView>()
            .FirstOrDefault();
        detailView.Should().NotBeNull("ProjectDetailView should be in the visual tree");
        detailView!.DataContext.Should().Be(project, "DataContext should be the selected project");

        // Verify project metadata is populated on the ViewModel
        project.ProjectName.Should().NotBeNullOrWhiteSpace("ProjectName should be set");
        project.Target.Should().NotBeNullOrWhiteSpace("Target should be set");
        project.StartDate.Should().NotBeNullOrWhiteSpace("StartDate should be set");
        project.EndDate.Should().NotBeNullOrWhiteSpace("EndDate should be set");
        project.PenaltyDays.Should().NotBeNullOrWhiteSpace("PenaltyDays should be set");
        project.Stages.Should().NotBeNull("Stages collection should exist");
        project.ProjectId.Should().NotBeNullOrWhiteSpace("ProjectId should be set");
        project.FounderKey.Should().NotBeNull("FounderKey should be initialized");

        window.Close();
    }

    [AvaloniaFact]
    public async Task ProjectDetail_ShowsCorrectTypeTerminology()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        var project = vm!.Projects[0];
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();

        // Verify type-dependent terminology is consistent
        if (project.ProjectType == "Fund")
        {
            project.ActionButtonText.Should().Contain("Fund", "Fund-type should have fund action text");
            project.InvestorLabel.Should().Be("Funders", "Fund-type should use 'Funders' label");
            project.TargetLabel.Should().Be("Goal:", "Fund-type should use 'Goal:' label");
        }
        else if (project.ProjectType == "Invest")
        {
            project.ActionButtonText.Should().Contain("Invest", "Invest-type should have invest action text");
            project.InvestorLabel.Should().Be("Investors", "Invest-type should use 'Investors' label");
            project.TargetLabel.Should().Be("Target:", "Invest-type should use 'Target:' label");
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task ProjectDetail_ShowsInvestButton_WhenProjectIsOpen()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        // Find an open project
        var openProject = vm!.Projects.FirstOrDefault(p => p.IsOpen);
        openProject.Should().NotBeNull("should have at least one open project");

        vm.OpenProjectDetail(openProject!);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        // The InvestButton border should be visible
        var detailView = window.GetVisualDescendants()
            .OfType<ProjectDetailView>()
            .FirstOrDefault();
        detailView.Should().NotBeNull();

        var investBtn = detailView!.FindControl<Border>("InvestButton");
        investBtn.Should().NotBeNull("InvestButton should exist in the detail view");
        investBtn!.IsVisible.Should().BeTrue("InvestButton should be visible for an open project");

        window.Close();
    }

    [AvaloniaFact]
    public async Task ProjectDetail_HasInvestedFlag_ReflectsPortfolioState()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        // Initially, projects loaded fresh should have HasInvested = false
        // (since this is a clean test profile with no portfolio)
        var project = vm!.Projects[0];
        project.HasInvested.Should().BeFalse(
            "fresh test profile should not have invested in any project");
        project.IsOpenAndNotInvested.Should().Be(project.IsOpen,
            "IsOpenAndNotInvested should match IsOpen when HasInvested is false");

        window.Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Invest Page — Form
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public async Task InvestPage_InitialState_ShowsInvestForm()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        var project = vm!.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = vm.InvestPageViewModel;
        investVm.Should().NotBeNull("InvestPageViewModel should be created");
        investVm!.CurrentScreen.Should().Be(InvestScreen.InvestForm, "initial screen should be InvestForm");
        investVm.IsInvestForm.Should().BeTrue();
        investVm.IsWalletSelector.Should().BeFalse();
        investVm.IsInvoice.Should().BeFalse();
        investVm.IsSuccess.Should().BeFalse();
        investVm.InvestmentAmount.Should().BeEmpty("amount should start empty");

        window.Close();
    }

    [AvaloniaFact]
    public async Task InvestPage_QuickAmountButtons_SetInvestmentAmount()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        var project = vm!.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = vm.InvestPageViewModel!;

        // Select the 0.01 quick amount
        investVm.SelectQuickAmount(0.01);
        Dispatcher.UIThread.RunJobs();

        investVm.InvestmentAmount.Should().Be("0.01", "quick amount should set investment amount");
        investVm.SelectedQuickAmount.Should().Be(0.01);
        investVm.CanSubmit.Should().BeTrue("0.01 is above minimum (0.001)");

        // Stages should recompute with the new amount
        if (investVm.Stages.Count > 0)
        {
            investVm.Stages.Should().AllSatisfy(s =>
                s.Amount.Should().NotBe("0.00000000", "stage amounts should update with investment amount"));
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task InvestPage_ManualAmount_UpdatesStagesAndTotals()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        var project = vm!.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = vm.InvestPageViewModel!;

        // Set manual amount
        investVm.InvestmentAmount = "0.05";
        Dispatcher.UIThread.RunJobs();

        investVm.FormattedAmount.Should().Be("0.05000000", "formatted amount should pad to 8 decimals");
        investVm.CanSubmit.Should().BeTrue("0.05 is above minimum");

        // TotalAmount should include fees
        investVm.TotalAmount.Should().NotBeNullOrWhiteSpace("TotalAmount should be computed");
        // Total = amount + miner fee + angor fee, so it should be greater than the raw amount
        double.TryParse(investVm.TotalAmount.Split(' ')[0],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var total).Should().BeTrue("TotalAmount should be parseable");
        total.Should().BeGreaterThan(0.05, "total should include fees on top of the amount");

        window.Close();
    }

    [AvaloniaFact]
    public async Task InvestPage_Submit_DisabledBelowMinimum_EnabledAtMinimum()
    {
        using var profileScope = TestProfileScope.For(nameof(FindProjectsPanelTests));
        var window = TestHelpers.CreateShellWindow();

        window.NavigateToSection("Find Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        var vm = GetFindProjectsViewModel(window);
        await WaitForProjects(vm!);

        var project = vm!.Projects.First(p => p.IsOpen);
        vm.OpenProjectDetail(project);
        Dispatcher.UIThread.RunJobs();
        vm.OpenInvestPage();
        Dispatcher.UIThread.RunJobs();

        var investVm = vm.InvestPageViewModel!;

        // Below minimum — CanSubmit should be false
        investVm.InvestmentAmount = "0.0001";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeFalse("0.0001 is below minimum (0.001)");

        // Submit should not advance screen
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.InvestForm,
            "Submit should stay on InvestForm when CanSubmit is false");

        // At minimum — CanSubmit should be true
        investVm.InvestmentAmount = "0.001";
        Dispatcher.UIThread.RunJobs();
        investVm.CanSubmit.Should().BeTrue("0.001 is the minimum investment amount");

        // Submit should advance to WalletSelector
        investVm.Submit();
        Dispatcher.UIThread.RunJobs();
        investVm.CurrentScreen.Should().Be(InvestScreen.WalletSelector,
            "Submit should advance to WalletSelector when CanSubmit is true");

        window.Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static FindProjectsViewModel? GetFindProjectsViewModel(Window window)
    {
        var view = window.GetVisualDescendants()
            .OfType<FindProjectsView>()
            .FirstOrDefault();
        return view?.DataContext as FindProjectsViewModel;
    }

    private static (Visual? list, Panel? detail, Panel? invest) GetPanels(Window window)
    {
        var view = window.GetVisualDescendants()
            .OfType<FindProjectsView>()
            .FirstOrDefault();

        if (view == null) return (null, null, null);

        // ProjectListPanel is a ScrollableView (ContentControl), not a Panel
        var list = view.FindControl<ScrollableView>("ProjectListPanel") as Visual;
        var detail = view.FindControl<Panel>("ProjectDetailPanel");
        var invest = view.FindControl<Panel>("InvestPagePanel");
        return (list, detail, invest);
    }

    /// <summary>
    /// Wait for projects to load from SDK (async on construction).
    /// Polls until Projects.Count > 0 or timeout.
    /// </summary>
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

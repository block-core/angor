using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Shell;
using App.UI.Sections.Funders;
using App.UI.Sections.MyProjects;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace App.Test.Integration.Helpers;

/// <summary>
/// High-level test helpers for driving the full Avalonia app in headless mode.
/// All interactions go through the visual tree using AutomationIds, simulating
/// real user behavior and making tests portable to Appium E2E tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a headless Window containing the real ShellView (resolved from DI).
    /// The ShellView constructor sets its own DataContext to ShellViewModel from App.Services.
    /// </summary>
    public static Window CreateShellWindow(int width = 1280, int height = 800)
    {
        var shellView = new ShellView();
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = shellView,
        };
        window.Show();

        // Force a layout pass so the visual tree is fully built
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>
    /// Gets the ShellViewModel from the ShellView inside the window.
    /// </summary>
    public static ShellViewModel GetShellViewModel(this Window window)
    {
        var shellView = window.FindDescendantOfType<ShellView>()
                        ?? throw new InvalidOperationException("ShellView not found in window");
        return (ShellViewModel)shellView.DataContext!;
    }

    /// <summary>
    /// Waits until a control with the given AutomationId appears in the visual tree,
    /// polling at the given interval. Returns null on timeout.
    /// </summary>
    public static async Task<T?> WaitForControl<T>(
        this Visual root,
        string automationId,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null) where T : Visual
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            // Process pending UI work
            Dispatcher.UIThread.RunJobs();

            var found = root.FindByAutomationId<T>(automationId);
            if (found != null && found.IsVisible)
                return found;

            await Task.Delay(interval);
        }

        return null;
    }

    /// <summary>
    /// Waits until a condition becomes true, polling at the given interval.
    /// </summary>
    public static async Task<bool> WaitForCondition(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            if (condition())
                return true;

            await Task.Delay(interval);
        }

        return false;
    }

    /// <summary>
    /// Clicks a button found by AutomationId. Raises the Button.Click routed event.
    /// </summary>
    public static async Task ClickButton(this Visual root, string automationId, TimeSpan? timeout = null)
    {
        var button = await root.WaitForControl<Button>(automationId, timeout)
                     ?? throw new TimeoutException($"Button '{automationId}' not found within timeout");

        // Raise the Click event the same way Avalonia does internally
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Types text into a TextBox found by AutomationId.
    /// </summary>
    public static async Task TypeText(this Visual root, string automationId, string text, TimeSpan? timeout = null)
    {
        var textBox = await root.WaitForControl<TextBox>(automationId, timeout)
                      ?? throw new TimeoutException($"TextBox '{automationId}' not found within timeout");

        textBox.Text = text;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Gets the text from a TextBlock found by AutomationId.
    /// </summary>
    public static async Task<string?> GetText(this Visual root, string automationId, TimeSpan? timeout = null)
    {
        var textBlock = await root.WaitForControl<TextBlock>(automationId, timeout);
        return textBlock?.Text;
    }

    /// <summary>
    /// Navigates to a section by selecting the corresponding NavItem in the ShellViewModel.
    /// </summary>
    public static void NavigateToSection(this Window window, string sectionLabel)
    {
        var vm = window.GetShellViewModel();
        var navItem = vm.NavEntries.OfType<NavItem>().FirstOrDefault(n => n.Label == sectionLabel)
                      ?? throw new InvalidOperationException($"Nav item '{sectionLabel}' not found");

        vm.SelectedNavItem = navItem;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Navigates to Settings by calling ShellViewModel.NavigateToSettings().
    /// </summary>
    public static void NavigateToSettings(this Window window)
    {
        var vm = window.GetShellViewModel();
        vm.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Opens a modal by showing the given content via ShellViewModel.ShowModal().
    /// </summary>
    public static void ShowModal(this Window window, Control content)
    {
        var vm = window.GetShellViewModel();
        vm.ShowModal(content);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Closes the current modal via ShellViewModel.HideModal().
    /// </summary>
    public static void HideModal(this Window window)
    {
        var vm = window.GetShellViewModel();
        vm.HideModal();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Finds the first WalletCard in the visual tree and returns it.
    /// Useful for finding wallet action buttons that are inside template parts.
    /// </summary>
    public static async Task<Visual?> WaitForWalletCard(
        this Visual root,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();

            // WalletCard buttons have AutomationIds like "WalletCardBtnSend"
            var btn = root.FindByAutomationId<Button>("WalletCardBtnSend");
            if (btn != null && btn.IsVisible)
                return btn;

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return null;
    }

    /// <summary>
    /// Finds a control by x:Name in the visual tree (not AutomationId).
    /// Useful for controls that don't have AutomationIds but have x:Name.
    /// </summary>
    public static T? FindByName<T>(this Visual root, string name) where T : Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(c => c.Name == name);
    }

    /// <summary>
    /// Drives the real recovery UI flow for an investment: opens detail,
    /// clicks the recovery button, confirms the modal, confirms the fee popup,
    /// and waits for the recovery action to complete.
    /// </summary>
    public static async Task ClickRecoveryFlowAsync(
        this Window window,
        PortfolioViewModel portfolioVm,
        InvestmentViewModel investment,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        portfolioVm.OpenInvestmentDetail(investment);
        Dispatcher.UIThread.RunJobs();

        var detailOpened = await WaitForCondition(
            () => ReferenceEquals(portfolioVm.SelectedInvestment, investment),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!detailOpened)
            throw new TimeoutException("Portfolio selected investment detail did not open");

        var detailViewVisible = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<InvestmentDetailView>().Any(v => v.IsVisible),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!detailViewVisible)
            throw new TimeoutException("InvestmentDetailView did not appear");

        var recoverButton = window.FindByName<Button>("RecoverFundsButton")
            ?? throw new InvalidOperationException("RecoverFundsButton not found in detail view");
        recoverButton.IsVisible.Should().BeTrue("RecoverFundsButton should be visible before clicking it");

        recoverButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, recoverButton));
        Dispatcher.UIThread.RunJobs();

        var modalOpened = await WaitForCondition(
            () => investment.ShowRecoveryModal || investment.ShowReleaseModal || investment.ShowClaimModal,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!modalOpened)
            throw new TimeoutException("Recovery modal did not open");

        var confirmButton = window.FindByName<Button>("ConfirmRecoveryModal")
            ?? window.FindByName<Button>("ConfirmReleaseModal")
            ?? window.FindByName<Button>("ClaimPenaltyButton")
            ?? throw new InvalidOperationException("No visible recovery confirm button found");

        confirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, confirmButton));
        Dispatcher.UIThread.RunJobs();

        var feeConfirmButton = await window.WaitForControl<Button>("FeeConfirmButton", maxWait)
            ?? throw new TimeoutException("FeeSelectionPopup did not appear");

        feeConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, feeConfirmButton));
        Dispatcher.UIThread.RunJobs();

        var completed = await WaitForCondition(
            () => !investment.IsProcessing && (investment.ShowSuccessModal || !string.IsNullOrEmpty(investment.ErrorMessage)),
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMilliseconds(200));
        if (!completed)
            throw new TimeoutException("Recovery UI flow did not complete");

        investment.ErrorMessage.Should().BeNullOrEmpty("Recovery flow should complete without UI error");
        investment.ShowSuccessModal.Should().BeTrue("Recovery success modal should be shown after successful recovery");
    }

    public static async Task ClickApproveSignatureAsync(
        this Window window,
        FundersViewModel fundersVm,
        SignatureRequestViewModel signature,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(15);

        window.NavigateToSection("Funders");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        fundersVm.SetFilter("waiting");
        Dispatcher.UIThread.RunJobs();

        var buttonReady = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<Button>().Any(b =>
                b.IsVisible &&
                b.Name == "ApproveButton" &&
                b.Tag is int tag &&
                tag == signature.Id),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!buttonReady)
            throw new TimeoutException($"ApproveButton for signature {signature.Id} did not appear");

        var approveButton = window.GetVisualDescendants().OfType<Button>().First(b =>
            b.IsVisible &&
            b.Name == "ApproveButton" &&
            b.Tag is int tag &&
            tag == signature.Id);

        approveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, approveButton));
        Dispatcher.UIThread.RunJobs();
    }

    public static async Task ClickInvestmentDetailActionAsync(
        this Window window,
        PortfolioViewModel portfolioVm,
        InvestmentViewModel investment,
        string buttonName,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("Funded");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        portfolioVm.OpenInvestmentDetail(investment);
        Dispatcher.UIThread.RunJobs();

        var detailOpened = await WaitForCondition(
            () => ReferenceEquals(portfolioVm.SelectedInvestment, investment),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!detailOpened)
            throw new TimeoutException("Portfolio selected investment detail did not open");

        var buttonVisible = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<Button>().Any(b => b.IsVisible && b.Name == buttonName),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!buttonVisible)
            throw new TimeoutException($"Investment detail action button '{buttonName}' did not appear");

        var button = window.GetVisualDescendants().OfType<Button>().First(b => b.IsVisible && b.Name == buttonName);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();

        var actionCompleted = await WaitForCondition(
            () => !investment.IsProcessing,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!actionCompleted)
            throw new TimeoutException($"Investment detail action '{buttonName}' did not complete");
    }

    public static async Task ClickManageProjectClaimStageAsync(
        this Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project,
        int stageNumber,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();

        var manageOpened = await WaitForCondition(
            () => myProjectsVm.SelectedManageProject != null,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!manageOpened)
            throw new TimeoutException("Manage project view did not open");

        var claimButtonVisible = await WaitForCondition(
            () => window.GetVisualDescendants().OfType<Button>().Any(b =>
                b.IsVisible &&
                b.Classes.Contains("StageClaimBtn") &&
                b.Tag is int tag &&
                tag == stageNumber),
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!claimButtonVisible)
            throw new TimeoutException($"Claim button for stage {stageNumber} did not appear");

        var claimButton = window.GetVisualDescendants().OfType<Button>().First(b =>
            b.IsVisible &&
            b.Classes.Contains("StageClaimBtn") &&
            b.Tag is int tag &&
            tag == stageNumber);
        claimButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, claimButton));
        Dispatcher.UIThread.RunJobs();

        var claimSelectedBtn = await WaitForCondition(
            () => window.FindByName<Button>("ClaimSelectedBtn")?.IsVisible == true,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!claimSelectedBtn)
            throw new TimeoutException("ClaimSelectedBtn did not appear");

        var manageVm = myProjectsVm.SelectedManageProject!;

        var selectionReady = await WaitForCondition(
            () => manageVm.SelectedStage?.AvailableTransactions.Count > 0,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!selectionReady)
            throw new TimeoutException("Claim UTXO selection list did not populate");

        foreach (var tx in manageVm.SelectedStage!.AvailableTransactions)
            tx.IsSelected = true;
        Dispatcher.UIThread.RunJobs();

        var claimSelected = window.FindByName<Button>("ClaimSelectedBtn")!;
        claimSelected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, claimSelected));
        Dispatcher.UIThread.RunJobs();

        var feeConfirmButton = await window.WaitForControl<Button>("FeeConfirmButton", maxWait)
            ?? throw new TimeoutException("FeeSelectionPopup did not appear for claim flow");
        feeConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, feeConfirmButton));
        Dispatcher.UIThread.RunJobs();

        var completed = await WaitForCondition(
            () => !manageVm.IsClaiming && manageVm.ShowSuccessModal,
            TimeSpan.FromMinutes(3),
            TimeSpan.FromMilliseconds(200));
        if (!completed)
            throw new TimeoutException("Claim stage flow did not complete");
    }

    public static async Task<bool> ClickManageProjectReleaseFundsAsync(
        this Window window,
        MyProjectsViewModel myProjectsVm,
        MyProjectItemViewModel project,
        TimeSpan? timeout = null)
    {
        var maxWait = timeout ?? TimeSpan.FromSeconds(30);

        window.NavigateToSection("My Projects");
        await Task.Delay(500);
        Dispatcher.UIThread.RunJobs();

        myProjectsVm.OpenManageProject(project);
        Dispatcher.UIThread.RunJobs();

        var manageOpened = await WaitForCondition(
            () => myProjectsVm.SelectedManageProject != null,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!manageOpened)
            throw new TimeoutException("Manage project view did not open");

        var manageVm = myProjectsVm.SelectedManageProject!;

        var manageView = window.GetVisualDescendants().OfType<ManageProjectView>().FirstOrDefault(v => v.IsVisible)
            ?? throw new InvalidOperationException("Visible ManageProjectView not found");
        manageView.OpenReleaseFundsModal();
        Dispatcher.UIThread.RunJobs();

        var releaseButtonVisible = await WaitForCondition(
            () => window.FindByName<Button>("ReleaseFundsConfirmBtn")?.IsVisible == true,
            maxWait,
            TimeSpan.FromMilliseconds(100));
        if (!releaseButtonVisible)
            throw new TimeoutException("ReleaseFundsConfirmBtn did not appear");

        var releaseButton = window.FindByName<Button>("ReleaseFundsConfirmBtn")!;
        releaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, releaseButton));
        Dispatcher.UIThread.RunJobs();

        var completed = await WaitForCondition(
            () => !manageVm.IsReleasingFunds,
            maxWait,
            TimeSpan.FromMilliseconds(200));
        if (!completed)
            throw new TimeoutException("Release funds flow did not complete");

        return manageVm.ShowReleaseFundsSuccessModal;
    }
}

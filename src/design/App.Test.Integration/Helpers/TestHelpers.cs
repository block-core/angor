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
}

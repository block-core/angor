using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Sections.MyProjects;
using App.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Regression tests for responsive-layout plumbing:
///
///  1. Views must adapt when the window crosses the compact breakpoint in BOTH
///     directions (desktop → mobile → desktop).
///  2. Views must KEEP adapting after being detached and re-attached from the
///     logical tree — sections are cached and re-attached on every section switch,
///     and a bug where OnDetachedFromLogicalTree disposed the LayoutModeService
///     subscription without re-creating it on attach silently killed all
///     responsiveness after the first section switch (desktop resize did nothing,
///     mobile showed stale desktop chrome).
/// </summary>
public class ResponsivenessRegressionTests
{
    [AvaloniaFact]
    public void ManageProjectContentView_toggles_compact_layout_both_directions()
    {
        var (view, window) = CreateManageView(1280);
        try
        {
            view.Classes.Contains("Compact").Should().BeFalse("desktop width must not apply Compact");
            StageActions(view)!.Orientation.Should().Be(Orientation.Horizontal);

            // → mobile
            SetWidth(window, 390);
            view.Classes.Contains("Compact").Should().BeTrue("mobile width must apply Compact");
            StageActions(view)!.Orientation.Should().Be(Orientation.Vertical,
                "stage action buttons must stack vertically on mobile");

            // → back to desktop
            SetWidth(window, 1280);
            view.Classes.Contains("Compact").Should().BeFalse("desktop responsiveness must not be lost after visiting mobile");
            StageActions(view)!.Orientation.Should().Be(Orientation.Horizontal);
        }
        finally
        {
            Cleanup(window);
        }
    }

    [AvaloniaFact]
    public void ManageProjectContentView_stays_responsive_after_detach_and_reattach()
    {
        var (view, window) = CreateManageView(1280);
        try
        {
            // Simulate a section switch: detach the view, then re-attach it.
            var host = (ContentControl)window.Content!;
            var section = host.Content; // the ScrollViewer hosting the view
            host.Content = null;
            Dispatcher.UIThread.RunJobs();
            host.Content = section;
            Dispatcher.UIThread.RunJobs();

            // The responsive subscription must survive (be re-created) — resize must still work.
            SetWidth(window, 390);
            view.Classes.Contains("Compact").Should().BeTrue(
                "views are cached and re-attached on section switches — responsiveness must survive a detach/re-attach cycle");

            SetWidth(window, 1280);
            view.Classes.Contains("Compact").Should().BeFalse();
        }
        finally
        {
            Cleanup(window);
        }
    }

    [AvaloniaFact]
    public void MyProjectsView_sidebar_strips_desktop_chrome_on_mobile()
    {
        LayoutModeService.Instance.UpdateWidth(1280);

        var vm = global::App.App.Services.GetRequiredService<MyProjectsViewModel>();
        var view = new MyProjectsView(vm);
        var window = new Window { Width = 1280, Height = 900, Content = new ContentControl { Content = view } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var sidebar = view.FindControl<Border>("MyProjectsSidebar");
            sidebar.Should().NotBeNull();

            SetWidth(window, 390);
            // On mobile the sidebar hero card must not render as an empty Surface box:
            // background/border stripped, hero elements hidden, mobile actions shown.
            sidebar!.Background.Should().BeNull("mobile sidebar must not render an empty background card");
            view.FindControl<Grid>("SidebarStats")!.IsVisible.Should().BeFalse();
            view.FindControl<StackPanel>("MobileActionPanel")!.IsVisible.Should().BeTrue();

            SetWidth(window, 1280);
            sidebar.Background.Should().NotBeNull("desktop sidebar chrome must come back");
            view.FindControl<Grid>("SidebarStats")!.IsVisible.Should().BeTrue();
        }
        finally
        {
            Cleanup(window);
        }
    }

    /// <summary>
    /// Views that subscribe to LayoutModeService and dispose the subscription in
    /// OnDetachedFromLogicalTree MUST re-create it in OnAttachedToLogicalTree —
    /// sections are cached and re-attached on every section switch, and a missing
    /// re-subscribe silently kills responsiveness for the rest of the session.
    /// This asserts the private _layoutSubscription field is repopulated after a
    /// detach/re-attach cycle for every responsive section view.
    /// </summary>
    [AvaloniaFact]
    public void Responsive_views_recreate_layout_subscription_after_detach_and_reattach()
    {
        LayoutModeService.Instance.UpdateWidth(1280);

        var views = new Control[]
        {
            new global::App.UI.Sections.Home.HomeView(new global::App.UI.Sections.Home.HomeViewModel()),
            new global::App.UI.Sections.FindProjects.FindProjectsView(
                global::App.App.Services.GetRequiredService<global::App.UI.Sections.FindProjects.FindProjectsViewModel>()),
            new global::App.UI.Sections.FindProjects.InvestPageView(),
            new global::App.UI.Sections.Funds.FundsView(
                global::App.App.Services.GetRequiredService<global::App.UI.Sections.Funds.FundsViewModel>()),
            new global::App.UI.Sections.Settings.SettingsView(
                global::App.App.Services.GetRequiredService<global::App.UI.Sections.Settings.SettingsViewModel>()),
            new global::App.UI.Sections.Portfolio.PortfolioView(
                global::App.App.Services.GetRequiredService<global::App.UI.Sections.Portfolio.PortfolioViewModel>()),
            new CreateProjectView
            {
                DataContext = global::App.App.Services.GetRequiredService<CreateProjectViewModel>(),
            },
            new global::App.UI.Sections.MyProjects.EditProfile.EditProfileView(),
        };

        foreach (var view in views)
        {
            var window = new Window { Width = 1280, Height = 900, Content = new ContentControl { Content = view } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var field = view.GetType().GetField("_layoutSubscription",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                field.Should().NotBeNull(
                    $"{view.GetType().Name} is expected to keep its LayoutModeService subscription in _layoutSubscription");

                // Simulate a section switch: detach, then re-attach.
                var host = (ContentControl)window.Content!;
                host.Content = null;
                Dispatcher.UIThread.RunJobs();
                host.Content = view;
                Dispatcher.UIThread.RunJobs();

                field!.GetValue(view).Should().NotBeNull(
                    $"{view.GetType().Name} must re-create its layout subscription on re-attach — " +
                    "views are cached across section switches and a dead subscription kills responsiveness");
            }
            finally
            {
                Cleanup(window);
            }
        }
    }

    // ── helpers ──

    private static (ManageProjectContentView view, Window window) CreateManageView(double width)
    {
        LayoutModeService.Instance.UpdateWidth(width);

        var factory = global::App.App.Services
            .GetRequiredService<Func<MyProjectItemViewModel, ManageProjectViewModel>>();
        var vm = factory(new MyProjectItemViewModel { Name = "Responsive Test", ProjectType = "fund" });
        vm.Stages.Add(new ManageStageViewModel
        {
            Number = 1, AmountLeft = "0.1", UtxoCount = 2,
            Available = true, CanClaim = true, UnspentTransactionCount = 1,
        });

        var view = new ManageProjectContentView { DataContext = vm };
        var window = new Window
        {
            Width = width, Height = 900,
            Content = new ContentControl { Content = new ScrollViewer { Content = view } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    private static StackPanel? StageActions(Control view) =>
        view.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(s => s.Classes.Contains("StageActions"));

    /// <summary>Drives LayoutModeService + window size the way ShellView does on resize.</summary>
    private static void SetWidth(Window window, double width)
    {
        window.Width = width;
        LayoutModeService.Instance.UpdateWidth(width);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Cleanup(Window window)
    {
        window.Close();
        LayoutModeService.Instance.UpdateWidth(1280);
        Dispatcher.UIThread.RunJobs();
    }
}

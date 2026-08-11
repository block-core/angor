using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using App.UI.Sections.FindProjects;
using App.UI.Sections.Funders;
using App.UI.Sections.Funds;
using App.UI.Sections.Home;
using App.UI.Sections.MyProjects;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Sections.Settings;
using App.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Optris.Icons.Avalonia;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Dark-mode icon regression tests.
///
/// Root cause guarded here: Optris.Icons.Avalonia.Icon bakes
/// <c>Foreground ?? Brushes.Black</c> into an IconImage snapshot whenever
/// Value/Foreground changes. Any icon that never receives a Foreground
/// (explicitly, via a matching style, or via inheritance) renders permanently
/// BLACK — invisible on dark backgrounds. The global default lives in
/// UI/Themes/V2/Styles/Icons.axaml (<c>Style Selector="i|Icon"</c>) and MUST
/// match everywhere, including control templates and popups.
///
/// These tests render key views under the Dark theme variant with the real
/// theme loaded, walk the visual tree, and fail on any icon whose effective
/// Foreground (or baked IconImage brush) is null or pure black.
/// </summary>
public class DarkModeIconTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Views under audit
    // ═══════════════════════════════════════════════════════════════════

    [AvaloniaFact]
    public void HomeView_icons_are_visible_in_dark_mode()
    {
        AuditIcons(new HomeView(new HomeViewModel()), nameof(HomeView));
    }

    [AvaloniaFact]
    public void SettingsView_icons_are_visible_in_dark_mode()
    {
        var vm = global::App.App.Services.GetRequiredService<SettingsViewModel>();
        AuditIcons(new SettingsView(vm), nameof(SettingsView));
    }

    [AvaloniaFact]
    public void FundsView_icons_are_visible_in_dark_mode()
    {
        var vm = global::App.App.Services.GetRequiredService<FundsViewModel>();
        AuditIcons(new FundsView(vm), nameof(FundsView));
    }

    [AvaloniaFact]
    public void FundersView_icons_are_visible_in_dark_mode()
    {
        var vm = global::App.App.Services.GetRequiredService<FundersViewModel>();
        AuditIcons(new FundersView(vm), nameof(FundersView));
    }

    [AvaloniaFact]
    public void FindProjectsView_icons_are_visible_in_dark_mode()
    {
        var vm = global::App.App.Services.GetRequiredService<FindProjectsViewModel>();
        vm.IsInitialLoad = false;
        vm.IsLoading = false;
        vm.HasMoreItems = true;
        AuditIcons(new FindProjectsView(vm), nameof(FindProjectsView));
    }

    [AvaloniaFact]
    public void ProjectDetailView_icons_are_visible_in_dark_mode()
    {
        var vm = new ProjectItemViewModel
        {
            ProjectName = "Icon Audit Project",
            ProjectType = "Fund",
            ProfileLoaded = true,
            ProjectId = "angor1qtest000000000000000000000000000000000",
        };
        AuditIcons(new ProjectDetailView { DataContext = vm }, nameof(ProjectDetailView));
    }

    /// <summary>Covers the markdown editor toolbar (Write/Preview, formatting buttons, guide).</summary>
    [AvaloniaFact]
    public void EditProfileView_icons_are_visible_in_dark_mode()
    {
        var factory = global::App.App.Services
            .GetRequiredService<Func<MyProjectItemViewModel, EditProfileViewModel>>();
        var vm = factory(new MyProjectItemViewModel
        {
            Name = "Icon Audit Project",
            ProjectType = "fund",
            ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
        });
        vm.SetActiveTab("project"); // realize the markdown editor toolbar

        AuditIcons(new EditProfileView { DataContext = vm }, nameof(EditProfileView));
    }

    /// <summary>Covers ManageProject stage cards incl. the syncing/claim affordances.</summary>
    [AvaloniaFact]
    public void ManageProjectContentView_icons_are_visible_in_dark_mode()
    {
        var factory = global::App.App.Services
            .GetRequiredService<Func<MyProjectItemViewModel, ManageProjectViewModel>>();
        var vm = factory(new MyProjectItemViewModel
        {
            Name = "Icon Audit Project",
            ProjectType = "fund",
            TargetAmount = "0.50000",
            ProjectIdentifier = "angor1qtest000000000000000000000000000000000",
        });
        vm.Stages.Add(new ManageStageViewModel
        {
            Number = 1, AmountLeft = "0.12345678", UtxoCount = 12,
            CompletionDate = "01 Jan 2027", Available = true, CanClaim = true,
            UnspentTransactionCount = 3, ClaimableTransactionCount = 2, TotalTransactionCount = 5,
        });

        AuditIcons(new ManageProjectContentView { DataContext = vm }, nameof(ManageProjectContentView));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Harness
    // ═══════════════════════════════════════════════════════════════════

    private static readonly PropertyInfo? IconImageProperty =
        typeof(Icon).GetProperty("Image", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    /// <summary>
    /// Hosts the view in a Dark-variant window, flushes layout/styles, then asserts
    /// every Icon in the visual tree has a usable (non-null, non-black) foreground
    /// AND that the baked IconImage brush is not the package's black fallback.
    /// </summary>
    private static void AuditIcons(Control view, string viewName)
    {
        var app = Application.Current!;
        var previousVariant = app.RequestedThemeVariant;
        app.RequestedThemeVariant = ThemeVariant.Dark;
        LayoutModeService.Instance.UpdateWidth(1280);

        var window = new Window
        {
            Width = 1280,
            Height = 900,
            RequestedThemeVariant = ThemeVariant.Dark,
            Content = new ScrollViewer { Content = view },
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var violations = new List<string>();

            foreach (var icon in view.GetVisualDescendants().OfType<Icon>())
            {
                var id = $"{viewName}: Icon '{icon.Value}' (Name='{icon.Name ?? "-"}')";

                // Zero-size guard: an effectively-visible icon squeezed to nothing
                // (e.g. fixed-size Button whose default Padding eats the content).
                if (icon.IsEffectivelyVisible && (icon.Bounds.Width < 1 || icon.Bounds.Height < 1))
                    violations.Add($"{id} is visible but has zero render size ({icon.Bounds.Width}x{icon.Bounds.Height}) — check container Padding/Width.");

                if (icon.Foreground is null)
                {
                    violations.Add($"{id} has NULL Foreground — bakes to the black fallback.");
                    continue;
                }

                if (IsPureBlack(icon.Foreground))
                    violations.Add($"{id} has pure-black Foreground in dark mode.");

                // Inspect the baked snapshot too: catches ordering holes where a
                // style exists but never re-fired the bake.
                if (GetBakedBrush(icon) is { } baked && IsPureBlack(baked) && !IsPureBlack(icon.Foreground))
                    violations.Add($"{id} baked IconImage brush is black despite Foreground '{icon.Foreground}' (bake ordering hole).");
            }

            violations.Should().BeEmpty(
                $"all icons in {viewName} must be visible in dark mode " +
                $"(default style: UI/Themes/V2/Styles/Icons.axaml 'i|Icon'):\n" +
                string.Join("\n", violations));
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousVariant;
            LayoutModeService.Instance.UpdateWidth(1280);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static IBrush? GetBakedBrush(Icon icon)
    {
        // Icon.Image is an internal IconImage with a public Brush styled property.
        var image = IconImageProperty?.GetValue(icon);
        if (image is null) return null;

        var brushProp = image.GetType().GetProperty("Brush", BindingFlags.Instance | BindingFlags.Public);
        return brushProp?.GetValue(image) as IBrush;
    }

    private static bool IsPureBlack(IBrush brush)
        => brush is ISolidColorBrush { Color: { R: 0, G: 0, B: 0, A: > 0 } };
}

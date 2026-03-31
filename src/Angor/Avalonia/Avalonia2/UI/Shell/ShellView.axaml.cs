using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace Avalonia2.UI.Shell;

/// <summary>
/// Converts a StreamGeometry resource key (e.g. "NavIconHome") to the actual StreamGeometry instance.
/// Used in the nav item DataTemplate to bind icon paths.
/// </summary>
public class NavIconConverter : IValueConverter
{
    public static readonly NavIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current!.TryFindResource(key, out var resource) && resource is StreamGeometry geometry)
        {
            return geometry;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public partial class ShellView : UserControl
{
    /// <summary>Heavy blur for modal backdrop — much more prominent than before.</summary>
    /// <remarks>PERF: Reduced on mobile (radius 8 vs 20) to avoid GPU strain.</remarks>
    private static readonly BlurEffect ModalBlurDesktop = new() { Radius = 20 };
    private static readonly BlurEffect ModalBlurMobile = new() { Radius = 8 };

    /// <summary>Animation duration matching Vue prototype modal-fade: 250ms.</summary>
    private static readonly TimeSpan AnimDuration = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Shared transitions applied to modal content controls for open/close animation.
    /// Uses TransformOperationsTransition for scale + DoubleTransition for opacity.
    /// </summary>
    private static readonly Transitions ModalTransitions = new()
    {
        new TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = AnimDuration,
            Easing = new CubicEaseOut(),
        },
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = AnimDuration,
            Easing = new CubicEaseOut(),
        },
    };

    /// <summary>Transitions for the backdrop border opacity fade.</summary>
    private static readonly Transitions BackdropTransitions = new()
    {
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = AnimDuration,
            Easing = new CubicEaseOut(),
        },
    };

    /// <summary>Transitions for the toast fade animation (Vue: 0.3s ease).</summary>
    private static readonly Transitions ToastTransitions = new()
    {
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new CubicEaseOut(),
        },
    };

    /// <summary>
    /// The current modal content control that has been added as a direct child
    /// of the ModalOverlay Panel. We track it so we can remove it on close.
    /// </summary>
    private Control? _currentModalChild;

    /// <summary>Guard to prevent re-entrant close animation.</summary>
    private bool _isClosing;

    // ── Cached controls ──
    private Grid _shellContent = null!;
    private Control _desktopLogo = null!;
    private DockPanel _desktopHeader = null!;
    private Border _desktopSidebar = null!;
    private Border _contentBorder = null!;
    private Border _bottomTabBar = null!;
    private Border _textureOverlay = null!;

    // ── Named controls for mobile tab bar ──
    private Button _tabHome = null!;
    private Button _tabInvestor = null!;
    private Button _tabFounder = null!;
    private Button _tabFunds = null!;
    private Button _tabSettings = null!;
    // Pill indicators removed — Vue-style color-only active state
    private Border _investorSubTabs = null!;
    private Border _founderSubTabs = null!;
    private Button _investorSubTabFind = null!;
    private Button _investorSubTabFunded = null!;
    private Button _founderSubTabMyProjects = null!;
    private Button _founderSubTabFunders = null!;

    // ── Named controls for mobile floating back bars ──
    private Border _investorBackBar = null!;
    private Border _investmentDetailBackBar = null!;
    private Border _manageFundsBackBar = null!;
    private TextBlock _investorCtaText = null!;

    private IDisposable? _layoutSubscription;
    private IDisposable? _detailStateSubscription;

    /// <summary>
    /// Extra bottom padding for Android gesture navigation bar safe area.
    /// On Android, the system nav bar overlaps the bottom ~24dp of the screen.
    /// </summary>
    private static readonly double AndroidSafeAreaBottom =
        OperatingSystem.IsAndroid() ? 24 : 0;

    public ShellView()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<ShellViewModel>();
        DataContext = vm;
        ShellService.Register(vm);

        // ── Resolve layout controls ──
        _shellContent = this.FindControl<Grid>("ShellContent")!;
        _desktopLogo = this.FindControl<Control>("DesktopLogo")!;
        _desktopHeader = this.FindControl<DockPanel>("DesktopHeader")!;
        _desktopSidebar = this.FindControl<Border>("DesktopSidebar")!;
        _contentBorder = this.FindControl<Border>("ContentBorder")!;
        _bottomTabBar = this.FindControl<Border>("BottomTabBar")!;
        _textureOverlay = this.FindControl<Border>("TextureOverlay")!;

        var modalOverlay = this.FindControl<Panel>("ModalOverlay")!;
        var backdrop = this.FindControl<Border>("ShellModalBackdrop")!;

        // ── Resolve mobile tab bar controls ──
        _tabHome = this.FindControl<Button>("TabHome")!;
        _tabInvestor = this.FindControl<Button>("TabInvestor")!;
        _tabFounder = this.FindControl<Button>("TabFounder")!;
        _tabFunds = this.FindControl<Button>("TabFunds")!;
        _tabSettings = this.FindControl<Button>("TabSettings")!;
        // Pill indicators removed — Vue-style color-only active state
        _investorSubTabs = this.FindControl<Border>("InvestorSubTabs")!;
        _founderSubTabs = this.FindControl<Border>("FounderSubTabs")!;
        _investorSubTabFind = this.FindControl<Button>("InvestorSubTabFind")!;
        _investorSubTabFunded = this.FindControl<Button>("InvestorSubTabFunded")!;
        _founderSubTabMyProjects = this.FindControl<Button>("FounderSubTabMyProjects")!;
        _founderSubTabFunders = this.FindControl<Button>("FounderSubTabFunders")!;

        // ── Resolve mobile floating back bar controls ──
        _investorBackBar = this.FindControl<Border>("InvestorBackBar")!;
        _investmentDetailBackBar = this.FindControl<Border>("InvestmentDetailBackBar")!;
        _manageFundsBackBar = this.FindControl<Border>("ManageFundsBackBar")!;
        _investorCtaText = this.FindControl<TextBlock>("InvestorCtaText")!;

        // ── Subscribe to layout mode changes — toggle desktop/compact elements ──
        ApplyShellLayout(!LayoutModeService.Instance.IsCompact);
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyShellLayout(!isCompact));

        // Apply backdrop transitions once
        backdrop.Transitions = BackdropTransitions;

        // ── React to MobileActiveTab changes — update tab bar active states ──
        // Rule #9: CSS class toggling only, no BrushTransition, no code-behind color logic.
        vm.WhenAnyValue(x => x.MobileActiveTab)
            .Subscribe(tab =>
            {
                // Toggle TabBarItemActive class on each tab button
                _tabHome.Classes.Set("TabBarItemActive", tab == "home");
                _tabInvestor.Classes.Set("TabBarItemActive", tab == "investor");
                _tabFounder.Classes.Set("TabBarItemActive", tab == "founder");
                _tabFunds.Classes.Set("TabBarItemActive", tab == "funds");
                _tabSettings.Classes.Set("TabBarItemActive", tab == "settings");

                // Toggle MD3 pill indicator active class
                // Pill indicators removed — active state is color-only via TabBarItemActive class

                // Sub-tab and back-bar visibility is handled by the detail state subscription below.
                // Trigger a re-evaluation by reading current detail state.
                UpdateCompactOverlays(vm);
            });

        // ── React to detail view state changes — toggle sub-tabs vs back bars ──
        // Vue: sub-tabs hidden when detail views are open; back bars shown instead.
        _detailStateSubscription = vm.WhenAnyValue(
                x => x.MobileActiveTab,
                x => x.IsProjectDetailOpen,
                x => x.IsInvestPageOpen,
                x => x.IsInvestmentDetailOpen,
                x => x.IsManageFundsOpen,
                x => x.IsCreatingProject)
            .Subscribe(_ => UpdateCompactOverlays(vm));

        // ── React to MobileInvestorSubTab changes — update sub-tab active states ──
        vm.WhenAnyValue(x => x.MobileInvestorSubTab)
            .Subscribe(subTab =>
            {
                _investorSubTabFind.Classes.Set("SubTabActive", subTab == "find-projects");
                _investorSubTabFunded.Classes.Set("SubTabActive", subTab == "investments");
            });

        // ── React to MobileFounderSubTab changes — update sub-tab active states ──
        vm.WhenAnyValue(x => x.MobileFounderSubTab)
            .Subscribe(subTab =>
            {
                _founderSubTabMyProjects.Classes.Set("SubTabActive", subTab == "my-projects");
                _founderSubTabFunders.Classes.Set("SubTabActive", subTab == "funders");
            });

        // React to ModalContent changes — manage the visual tree directly.
        // This replaces the XAML ContentPresenter binding which suffered from
        // an intermittent race: IsVisible and Content changing in the same
        // binding batch could cause Avalonia to skip the measure/arrange pass
        // for the new content, leaving the modal card invisible (especially
        // on second open, and more often in light mode).
        vm.WhenAnyValue(x => x.ModalContent)
            .Subscribe(content =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (content is Control control)
                    {
                        // ── OPEN ──
                        // Remove any previous modal child immediately (no close anim if replaced)
                        if (_currentModalChild != null)
                        {
                            _currentModalChild.Transitions = null;
                            modalOverlay.Children.Remove(_currentModalChild);
                            _currentModalChild = null;
                        }
                        _isClosing = false;

                        // Start at closed state: invisible + slightly scaled down
                        control.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                        control.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                        control.Opacity = 0;
                        control.RenderTransformOrigin = Avalonia.RelativePoint.Center;
                        control.RenderTransform = TransformOperations.Parse("scale(0.95)");

                        // Add transitions BEFORE adding to tree so Avalonia picks them up
                        control.Transitions = ModalTransitions;

                        // Backdrop starts invisible
                        backdrop.Opacity = 0;

                        // Add to visual tree
                        modalOverlay.Children.Add(control);
                        _currentModalChild = control;

                        // Make the overlay visible
                        modalOverlay.IsVisible = true;

                        // Apply blur to the shell grid (reduced on mobile for perf)
                        _shellContent.Effect = LayoutModeService.Instance.IsCompact
                            ? ModalBlurMobile : ModalBlurDesktop;

                        // Force layout so the initial state is rendered
                        modalOverlay.InvalidateMeasure();
                        modalOverlay.InvalidateArrange();

                        // Kick off open animation by setting target values on next frame
                        // (must be deferred so the initial state is committed first)
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            control.Opacity = 1;
                            control.RenderTransform = TransformOperations.Parse("scale(1)");
                            backdrop.Opacity = 1;
                        }, Avalonia.Threading.DispatcherPriority.Render);
                    }
                    else
                    {
                        // ── CLOSE ──
                        if (_currentModalChild != null && !_isClosing)
                        {
                            _isClosing = true;
                            var closingChild = _currentModalChild;

                            // Animate to closed state
                            closingChild.Opacity = 0;
                            closingChild.RenderTransform = TransformOperations.Parse("scale(0.95)");
                            backdrop.Opacity = 0;

                            // Wait for transition to finish, then clean up
                            _ = CleanupAfterClose(closingChild, modalOverlay);
                        }
                        else if (_currentModalChild == null)
                        {
                            // Nothing to animate, just hide
                            modalOverlay.IsVisible = false;
                            _shellContent.Effect = null;
                        }
                    }
                });
            });

        // Shell-level backdrop click-to-close
        backdrop.PointerPressed += OnBackdropPressed;

        // ── Toast notification animation ──
        // React to ToastMessage changes: fade in when set, fade out when cleared.
        var toastBorder = this.FindControl<Border>("ToastOverlay")!;
        toastBorder.Transitions = ToastTransitions;

        vm.WhenAnyValue(x => x.ToastMessage)
            .Subscribe(message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        // Show: make visible at opacity 0, then animate to 1
                        toastBorder.Opacity = 0;
                        toastBorder.IsVisible = true;
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            toastBorder.Opacity = 1;
                        }, Avalonia.Threading.DispatcherPriority.Render);
                    }
                    else
                    {
                        // Hide: animate opacity to 0, then set IsVisible=false after transition
                        toastBorder.Opacity = 0;
                        _ = HideToastAfterFadeAsync(toastBorder);
                    }
                });
            });
    }

    // ═══════════════════════════════════════════════════════════════
    // ADAPTIVE LAYOUT
    // Switches between desktop (sidebar+header) and compact (tab bar)
    // by adjusting the single Grid's structure and element visibility.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Apply the shell layout for desktop or compact mode.
    /// Desktop: sidebar column 208px, header row 42px, tab bar hidden.
    /// Compact: sidebar column 0px, header row 0px, tab bar visible.
    /// </summary>
    private void ApplyShellLayout(bool isDesktop)
    {
        if (_shellContent == null) return;

        // CRITICAL: Never replace ColumnDefinitions/RowDefinitions collections.
        // Only modify existing column/row widths. Replacing collections causes
        // Avalonia's layout engine to crash (SIGABRT) because child controls
        // briefly reference invalid column/row indices during the swap.
        //
        // The XAML Grid always has 2 columns and 3 rows:
        //   Desktop:  Col0=208 (sidebar), Col1=* (content) | Row0=42 (header), Row1=* (content), Row2=0 (no tab bar)
        //   Compact:  Col0=* (content),   Col1=0 (hidden)  | Row0=0 (no header), Row1=* (content), Row2=Auto (tab bar)
        var cols = _shellContent.ColumnDefinitions;
        var rows = _shellContent.RowDefinitions;

        if (isDesktop)
        {
            _shellContent.Margin = new Avalonia.Thickness(0, 24, 24, 24);
            _shellContent.ColumnSpacing = 0;
            _shellContent.RowSpacing = 20;

            // Sidebar 208px, content fills rest
            if (cols.Count >= 2) { cols[0].Width = new GridLength(208); cols[1].Width = GridLength.Star; }
            if (rows.Count >= 3) { rows[0].Height = new GridLength(42); rows[2].Height = new GridLength(0); }

            Grid.SetColumn(_contentBorder, 1);
            _contentBorder.Classes.Set("Panel", true);

            _desktopLogo.IsVisible = true;
            _desktopHeader.IsVisible = true;
            _desktopSidebar.IsVisible = true;

            _bottomTabBar.IsVisible = false;
            _textureOverlay.IsVisible = true;
            _investorSubTabs.IsVisible = false;
            _founderSubTabs.IsVisible = false;
            _investorBackBar.IsVisible = false;
            _investmentDetailBackBar.IsVisible = false;
            _manageFundsBackBar.IsVisible = false;
        }
        else
        {
            _shellContent.Margin = new Avalonia.Thickness(0);
            _shellContent.ColumnSpacing = 0;
            _shellContent.RowSpacing = 0;

            // Content fills full width, sidebar collapses to 0
            if (cols.Count >= 2) { cols[0].Width = GridLength.Star; cols[1].Width = new GridLength(0); }
            if (rows.Count >= 3) { rows[0].Height = new GridLength(0); rows[2].Height = GridLength.Auto; }

            Grid.SetColumn(_contentBorder, 0);
            _contentBorder.Classes.Set("Panel", false);

            _desktopLogo.IsVisible = false;
            _desktopHeader.IsVisible = false;
            _desktopSidebar.IsVisible = false;

            _bottomTabBar.IsVisible = true;
            _bottomTabBar.Padding = new Avalonia.Thickness(0, 0, 0, AndroidSafeAreaBottom);
            _textureOverlay.IsVisible = false;

            if (DataContext is ShellViewModel vm)
            {
                UpdateCompactOverlays(vm);
            }
        }

        // Column spans for overlay elements
        var subTabColSpan = isDesktop ? 2 : 1;
        Grid.SetColumnSpan(_investorSubTabs, subTabColSpan);
        Grid.SetColumnSpan(_founderSubTabs, subTabColSpan);
        Grid.SetColumnSpan(_investorBackBar, subTabColSpan);
        Grid.SetColumnSpan(_investmentDetailBackBar, subTabColSpan);
        Grid.SetColumnSpan(_manageFundsBackBar, subTabColSpan);
        Grid.SetColumnSpan(_bottomTabBar, isDesktop ? 2 : 1);
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        _detailStateSubscription?.Dispose();
        _detailStateSubscription = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // MOBILE TAB BAR CLICK HANDLERS
    // Vue: @click="handleMobileTabChange('home')" etc. in App.vue
    // ═══════════════════════════════════════════════════════════════

    private void OnTabHome(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleMobileTabChange("home");
    }

    private void OnTabInvestor(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleMobileTabChange("investor");
    }

    private void OnTabFounder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleMobileTabChange("founder");
    }

    private void OnTabFunds(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleMobileTabChange("funds");
    }

    private void OnTabSettings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleMobileTabChange("settings");
    }

    // ═══════════════════════════════════════════════════════════════
    // FLOATING SUB-TAB CLICK HANDLERS
    // Vue: @click="handleInvestorSubTabChange('find-projects')" etc.
    // ═══════════════════════════════════════════════════════════════

    private void OnInvestorSubTabFind(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleInvestorSubTabChange("find-projects");
    }

    private void OnInvestorSubTabFunded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleInvestorSubTabChange("investments");
    }

    private void OnFounderSubTabMyProjects(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleFounderSubTabChange("my-projects");
    }

    private void OnFounderSubTabFunders(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.HandleFounderSubTabChange("funders");
    }

    // ═══════════════════════════════════════════════════════════════
    // FLOATING BACK BAR CLICK HANDLERS
    // Vue: Back buttons + CTAs on the mobile floating bar
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Back from project detail or invest page.
    /// Vue (line 6203): back button in investor back bar.
    /// </summary>
    private void OnInvestorBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.BackFromInvestorDetail();
    }

    /// <summary>
    /// Invest/Submit CTA on the investor back bar.
    /// Vue: showInvestPage ? submit : navigate to invest page.
    /// </summary>
    private void OnInvestorCtaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.MobileInvestAction();
    }

    /// <summary>
    /// Share button on the investor back bar.
    /// Opens share modal for the currently selected project.
    /// </summary>
    private void OnInvestorShareClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Share is handled by the project detail view itself via shell modal.
        // For now, this is a no-op placeholder — the share action is context-dependent
        // and the ProjectDetailView already has its own share button.
    }

    /// <summary>
    /// "Back to Investments" button on the investment detail back bar.
    /// Vue (line 6234): full-width green back button.
    /// </summary>
    private void OnInvestmentDetailBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.BackFromInvestmentDetail();
    }

    /// <summary>
    /// "Back to My Projects" button on the manage funds back bar.
    /// Vue (line 6247): full-width green back button.
    /// </summary>
    private void OnManageFundsBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
            vm.CloseManageFundsFromShell();
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPACT OVERLAY VISIBILITY
    // Manages sub-tab panels and floating back bars based on
    // active tab + detail view state, only in compact mode.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Re-evaluate visibility of sub-tab panels and floating back bars.
    /// Called whenever MobileActiveTab or any detail state flag changes.
    /// Vue conditions (from App.vue):
    ///   Investor sub-tabs: mobileActiveTab === 'investor' && !isCreatingProject && !showProjectDetail && !showInvestPage && !showInvestmentDetail
    ///   Founder sub-tabs:  mobileActiveTab === 'founder' && !isCreatingProject && !showManageFunds
    ///   Investor back bar: (showProjectDetail || showInvestPage) && mobileActiveTab === 'investor'
    ///   Investment detail back bar: showInvestmentDetail && mobileActiveTab === 'investor' (currentPage === 'investments')
    ///   Manage funds back bar: showManageFunds && !isCreatingProject && mobileActiveTab === 'founder'
    /// </summary>
    private void UpdateCompactOverlays(ShellViewModel vm)
    {
        var isCompact = LayoutModeService.Instance.IsCompact;
        var tab = vm.MobileActiveTab;

        // ── Investor sub-tabs ──
        // Vue (line 6163): v-if="mobileActiveTab === 'investor' && !isCreatingProject && !showProjectDetail && !showInvestPage && !showInvestmentDetail"
        _investorSubTabs.IsVisible = isCompact
            && tab == "investor"
            && !vm.IsCreatingProject
            && !vm.IsProjectDetailOpen
            && !vm.IsInvestPageOpen
            && !vm.IsInvestmentDetailOpen;

        // ── Founder sub-tabs ──
        // Vue (line 6500): v-if="mobileActiveTab === 'founder' && !isCreatingProject && !showManageFunds"
        _founderSubTabs.IsVisible = isCompact
            && tab == "founder"
            && !vm.IsCreatingProject
            && !vm.IsManageFundsOpen;

        // ── Investor back bar (Back + Invest CTA + Share) ──
        // Vue (line 6203): v-if="(showProjectDetail || showInvestPage) && mobileActiveTab === 'investor'"
        _investorBackBar.IsVisible = isCompact
            && tab == "investor"
            && (vm.IsProjectDetailOpen || vm.IsInvestPageOpen);

        // Update CTA text: "Submit" when on invest page, "Invest" when on project detail
        if (_investorCtaText != null)
            _investorCtaText.Text = vm.IsInvestPageOpen ? "Submit" : "Invest";

        // ── Investment detail back bar ──
        // Vue (line 6234): v-if="showInvestmentDetail && currentPage === 'investments'"
        // currentPage === 'investments' maps to mobileActiveTab === 'investor' && mobileInvestorSubTab === 'investments'
        _investmentDetailBackBar.IsVisible = isCompact
            && vm.IsInvestmentDetailOpen
            && tab == "investor";

        // ── Manage funds back bar ──
        // Vue (line 6247): v-if="showManageFunds && selectedManageFundsProject && currentPage === 'my-projects' && !isCreatingProject"
        _manageFundsBackBar.IsVisible = isCompact
            && vm.IsManageFundsOpen
            && !vm.IsCreatingProject
            && tab == "founder";
    }

    // ═══════════════════════════════════════════════════════════════
    // MODAL LIFECYCLE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Wait for the close transition to finish, then remove the modal from the tree.
    /// Only hides the overlay/blur if no new modal was opened in the meantime
    /// (i.e., multi-step modal flows where ShowModal is called right after HideModal).
    /// </summary>
    private async Task CleanupAfterClose(Control closingChild, Panel modalOverlay)
    {
        // Wait for the transition duration + small buffer
        await Task.Delay(AnimDuration + TimeSpan.FromMilliseconds(50));

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            closingChild.Transitions = null;
            modalOverlay.Children.Remove(closingChild);

            // Only tear down the overlay if no replacement modal was opened
            if (_currentModalChild == closingChild || _currentModalChild == null)
            {
                _currentModalChild = null;
                modalOverlay.IsVisible = false;
                _shellContent.Effect = null;
            }
            _isClosing = false;
        });
    }

    /// <summary>
    /// Wait for the toast fade-out transition (300ms), then hide the border.
    /// If a new toast appeared in the meantime (Opacity went back to 1), skip hiding.
    /// </summary>
    private static async Task HideToastAfterFadeAsync(Border toastBorder)
    {
        await Task.Delay(350); // 300ms transition + 50ms buffer
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (toastBorder.Opacity < 0.01)
                toastBorder.IsVisible = false;
        });
    }

    /// <summary>
    /// Backdrop click — close the modal. Individual modal content views handle
    /// their own close logic via OnBackdropCloseRequested if they need custom behavior.
    /// </summary>
    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShellViewModel vm && vm.IsModalOpen)
        {
            // Notify the modal content that a backdrop close was requested.
            // The content can handle cleanup (e.g., resetting VM state) via IBackdropCloseable.
            if (vm.ModalContent is IBackdropCloseable closeable)
            {
                closeable.OnBackdropCloseRequested();
            }
            vm.HideModal();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Navigates to the Settings section when the header gear icon is clicked.
    /// </summary>
    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm)
        {
            vm.NavigateToSettings();
        }
    }

    /// <summary>
    /// Opens the wallet switcher modal when the header wallet button is clicked.
    /// Vue: showWalletModal = true on wallet-selector-header click.
    /// </summary>
    private void OnWalletSwitcherClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel vm && !vm.IsModalOpen)
        {
            var modal = new WalletSwitcherModal { DataContext = vm };
            vm.ShowModal(modal);
        }
    }

    /// <summary>
    /// Called when the ListBox prepares a container for an item.
    /// Applies the NavGroupHeaderItem theme to group header entries
    /// so they are non-selectable and visually distinct.
    /// </summary>
    public void OnNavContainerPreparing(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem item && item.DataContext is NavGroupHeader)
        {
            if (this.TryFindResource("NavGroupHeaderItem", out var theme) && theme is ControlTheme ct)
            {
                item.Theme = ct;
            }
        }
    }
}

/// <summary>
/// Interface for modal content views that need custom behavior when the backdrop is clicked.
/// </summary>
public interface IBackdropCloseable
{
    void OnBackdropCloseRequested();
}

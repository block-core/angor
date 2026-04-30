using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Angor.Sdk.Funding.Projects;
using Angor.Shared;
using Angor.Shared.Services;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Sections.FindProjects;

public partial class ProjectDetailView : UserControl
{
    private bool _detailsExpanded = false;
    private bool _nostrExpanded = false;
    private bool _navCtaVisible;
    private IDisposable? _layoutSubscription;

    // Cached FindControl results — avoid repeated tree walks
    private Button? _backBtn;
    private Border? _investBtn;
    private Border? _navCta;
    private ScrollViewer? _scroller;
    private Border? _detailsContainer;
    private Border? _nostrContainer;
    private Border? _progressFill;
    private Border? _shareBtn;
    private Border? _detailsHeader;
    private StackPanel? _detailsContent;
    private Control? _detailsChevron;
    private Border? _nostrHeader;
    private StackPanel? _nostrContent;
    private Control? _nostrChevron;

    // Responsive layout controls
    private Grid? _topSectionGrid;
    private Border? _topLeftCard;
    private Panel? _topRightCard;
    private Grid? _statsGrid;
    private Border? _statCard0;
    private Border? _statCard1;
    private Border? _statCard2;
    private Grid? _investInfoGrid;
    private Grid? _fundInfoGrid;
    private Grid? _subInfoGrid;
    private Border? _projectNamePill;
    private StackPanel? _contentStack;
    private DockPanel? _stickyNavBar;
    private Panel? _navSpacer;

    // Track the PropertyChanged handler to prevent accumulation
    private EventHandler<AvaloniaPropertyChangedEventArgs>? _parentPropertyChangedHandler;
    private Panel? _progressParent;

    public ProjectDetailView()
    {
        InitializeComponent();

        // Cache all controls once
        _backBtn = this.FindControl<Button>("BackButton");
        _investBtn = this.FindControl<Border>("InvestButton");
        _navCta = this.FindControl<Border>("NavCtaButton");
        _scroller = this.FindControl<ScrollViewer>("ContentScroller");
        _detailsContainer = this.FindControl<Border>("DetailsContainer");
        _nostrContainer = this.FindControl<Border>("NostrContainer");
        _progressFill = this.FindControl<Border>("ProgressFill");
        _shareBtn = this.FindControl<Border>("ShareButton");
        _detailsHeader = this.FindControl<Border>("DetailsHeader");
        _detailsContent = this.FindControl<StackPanel>("DetailsContent");
        _detailsChevron = this.FindControl<Control>("DetailsChevron");
        _nostrHeader = this.FindControl<Border>("NostrHeader");
        _nostrContent = this.FindControl<StackPanel>("NostrContent");
        _nostrChevron = this.FindControl<Control>("NostrChevron");

        // Back button
        if (_backBtn != null)
            _backBtn.Click += OnBackClick;

        // Reset scroll to top whenever the project changes
        DataContextChanged += OnDataContextChanged;

        // Invest button — navigate to InvestPage
        if (_investBtn != null)
            _investBtn.PointerPressed += OnInvestPressed;

        // Nav CTA button — same action as InvestButton
        if (_navCta != null)
            _navCta.PointerPressed += OnInvestPressed;

        // Share button — open share modal via shell
        if (_shareBtn != null)
            _shareBtn.PointerPressed += OnSharePressed;

        // Scroll detection for nav CTA fade
        if (_scroller != null)
            _scroller.ScrollChanged += OnScrollChanged;

        // Collapsible sections — single handler on each container
        if (_detailsContainer != null)
            _detailsContainer.PointerPressed += OnCollapsibleContainerPressed;

        if (_nostrContainer != null)
            _nostrContainer.PointerPressed += OnCollapsibleContainerPressed;

        // Copy buttons — Vue: copyToClipboard() on project ID, founder key, npub
        var copyProjectIdBtn = this.FindControl<Border>("CopyProjectIdBtn");
        if (copyProjectIdBtn != null)
            copyProjectIdBtn.PointerPressed += (_, ev) =>
            {
                if (DataContext is ProjectItemViewModel vm)
                    ClipboardHelper.CopyToClipboard(this, vm.ProjectId);
                ev.Handled = true;
            };

        var copyFounderKeyBtn = this.FindControl<Border>("CopyFounderKeyBtn");
        if (copyFounderKeyBtn != null)
            copyFounderKeyBtn.PointerPressed += (_, ev) =>
            {
                if (DataContext is ProjectItemViewModel vm)
                    ClipboardHelper.CopyToClipboard(this, vm.FounderKey);
                ev.Handled = true;
            };

        var copyNpubBtn = this.FindControl<Border>("CopyNpubBtn");
        if (copyNpubBtn != null)
            copyNpubBtn.PointerPressed += (_, ev) =>
            {
                if (DataContext is ProjectItemViewModel vm)
                    ClipboardHelper.CopyToClipboard(this, vm.NostrNpub);
                ev.Handled = true;
            };

        // Set progress bar width after loaded
        Loaded += OnLoaded;

        // Explorer link — open project txid in block explorer
        var explorerLink = this.FindControl<Border>("ExplorerLink");
        if (explorerLink != null)
            explorerLink.PointerPressed += OnExplorerLinkPressed;

        // View Project JSON button — show project info as formatted JSON
        var viewJsonBtn = this.FindControl<Border>("ViewProjectJsonBtn");
        if (viewJsonBtn != null)
            viewJsonBtn.PointerPressed += OnViewProjectJsonPressed;

        // Cache responsive layout controls
        _topSectionGrid = this.FindControl<Grid>("TopSectionGrid");
        _topLeftCard = this.FindControl<Border>("TopLeftCard");
        _topRightCard = this.FindControl<Panel>("TopRightCard");
        _statsGrid = this.FindControl<Grid>("StatsGrid");
        _statCard0 = this.FindControl<Border>("StatCard0");
        _statCard1 = this.FindControl<Border>("StatCard1");
        _statCard2 = this.FindControl<Border>("StatCard2");
        _investInfoGrid = this.FindControl<Grid>("InvestInfoGrid");
        _fundInfoGrid = this.FindControl<Grid>("FundInfoGrid");
        _subInfoGrid = this.FindControl<Grid>("SubInfoGrid");
        _projectNamePill = this.FindControl<Border>("ProjectNamePill");
        _contentStack = this.FindControl<StackPanel>("ContentStack");
        _stickyNavBar = this.FindControl<DockPanel>("StickyNavBar");
        _navSpacer = this.FindControl<Panel>("NavSpacer");

        // ── Responsive layout switching ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        // ── Top section: *,400 → stacked ──
        // XAML pre-declares ColumnDefinitions="*,400" RowDefinitions="Auto,Auto"
        if (_topSectionGrid != null && _topLeftCard != null && _topRightCard != null)
        {
            if (isCompact)
            {
                // Collapse col 1 to zero; stack cards in rows 0 and 1
                if (_topSectionGrid.ColumnDefinitions.Count >= 2)
                    _topSectionGrid.ColumnDefinitions[1].Width = new GridLength(0);

                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0, 0, 0, 24);

                Grid.SetColumn(_topRightCard, 0);
                Grid.SetRow(_topRightCard, 1);
            }
            else
            {
                // Restore 1fr + 400px columns, single row
                if (_topSectionGrid.ColumnDefinitions.Count >= 2)
                {
                    _topSectionGrid.ColumnDefinitions[0].Width = GridLength.Star;
                    _topSectionGrid.ColumnDefinitions[1].Width = new GridLength(400, GridUnitType.Pixel);
                }

                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0, 0, 24, 0);

                Grid.SetColumn(_topRightCard, 1);
                Grid.SetRow(_topRightCard, 0);
            }
        }

        // ── Stats grid: 3 cols with 16px spacers → stacked ──
        // XAML pre-declares ColumnDefinitions="*,16,*,16,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto"
        if (_statsGrid != null && _statCard0 != null && _statCard1 != null && _statCard2 != null)
        {
            if (isCompact)
            {
                // Collapse all gap + card columns except col 0
                if (_statsGrid.ColumnDefinitions.Count >= 5)
                {
                    _statsGrid.ColumnDefinitions[1].Width = new GridLength(0);
                    _statsGrid.ColumnDefinitions[2].Width = new GridLength(0);
                    _statsGrid.ColumnDefinitions[3].Width = new GridLength(0);
                    _statsGrid.ColumnDefinitions[4].Width = new GridLength(0);
                }

                Grid.SetColumn(_statCard0, 0); Grid.SetRow(_statCard0, 0);
                Grid.SetColumn(_statCard1, 0); Grid.SetRow(_statCard1, 1);
                Grid.SetColumn(_statCard2, 0); Grid.SetRow(_statCard2, 2);
                _statCard0.Margin = new Thickness(0, 0, 0, 12);
                _statCard1.Margin = new Thickness(0, 0, 0, 12);
                _statCard2.Margin = new Thickness(0);
            }
            else
            {
                // Restore star/16/star/16/star widths
                if (_statsGrid.ColumnDefinitions.Count >= 5)
                {
                    _statsGrid.ColumnDefinitions[0].Width = GridLength.Star;
                    _statsGrid.ColumnDefinitions[1].Width = new GridLength(16, GridUnitType.Pixel);
                    _statsGrid.ColumnDefinitions[2].Width = GridLength.Star;
                    _statsGrid.ColumnDefinitions[3].Width = new GridLength(16, GridUnitType.Pixel);
                    _statsGrid.ColumnDefinitions[4].Width = GridLength.Star;
                }

                Grid.SetColumn(_statCard0, 0); Grid.SetRow(_statCard0, 0);
                Grid.SetColumn(_statCard1, 2); Grid.SetRow(_statCard1, 0);
                Grid.SetColumn(_statCard2, 4); Grid.SetRow(_statCard2, 0);
                _statCard0.Margin = new Thickness(0);
                _statCard1.Margin = new Thickness(0);
                _statCard2.Margin = new Thickness(0);
            }
        }

        // ── Investment info grid: 2x2 → 4 stacked rows ──
        // XAML pre-declares ColumnDefinitions="*,16,*" RowDefinitions="Auto,16,Auto,16,Auto,16,Auto"
        if (_investInfoGrid != null)
        {
            var children = _investInfoGrid.Children.OfType<Border>().ToArray();
            if (isCompact)
            {
                // Collapse gap + right columns
                if (_investInfoGrid.ColumnDefinitions.Count >= 3)
                {
                    _investInfoGrid.ColumnDefinitions[1].Width = new GridLength(0);
                    _investInfoGrid.ColumnDefinitions[2].Width = new GridLength(0);
                }
                // Use 12px gaps between rows (rows 1, 3, 5)
                if (_investInfoGrid.RowDefinitions.Count >= 7)
                {
                    _investInfoGrid.RowDefinitions[1].Height = new GridLength(12, GridUnitType.Pixel);
                    _investInfoGrid.RowDefinitions[3].Height = new GridLength(12, GridUnitType.Pixel);
                    _investInfoGrid.RowDefinitions[5].Height = new GridLength(12, GridUnitType.Pixel);
                }

                // Stack all 4 cards in column 0 at rows 0, 2, 4, 6
                for (int i = 0; i < children.Length; i++)
                {
                    Grid.SetColumn(children[i], 0);
                    Grid.SetRow(children[i], i * 2);
                }
            }
            else
            {
                // Restore 2 cols with 16px gap
                if (_investInfoGrid.ColumnDefinitions.Count >= 3)
                {
                    _investInfoGrid.ColumnDefinitions[0].Width = GridLength.Star;
                    _investInfoGrid.ColumnDefinitions[1].Width = new GridLength(16, GridUnitType.Pixel);
                    _investInfoGrid.ColumnDefinitions[2].Width = GridLength.Star;
                }
                // Only row 1 is 16px gap; collapse rows 3, 5 (unused in 2x2)
                if (_investInfoGrid.RowDefinitions.Count >= 7)
                {
                    _investInfoGrid.RowDefinitions[1].Height = new GridLength(16, GridUnitType.Pixel);
                    _investInfoGrid.RowDefinitions[3].Height = new GridLength(0);
                    _investInfoGrid.RowDefinitions[5].Height = new GridLength(0);
                }

                if (children.Length >= 4)
                {
                    Grid.SetRow(children[0], 0); Grid.SetColumn(children[0], 0);
                    Grid.SetRow(children[1], 0); Grid.SetColumn(children[1], 2);
                    Grid.SetRow(children[2], 2); Grid.SetColumn(children[2], 0);
                    Grid.SetRow(children[3], 2); Grid.SetColumn(children[3], 2);
                }
            }
        }

        // ── Fund info grid: 2 cols → stacked ──
        // XAML pre-declares ColumnDefinitions="*,16,*" RowDefinitions="Auto,12,Auto"
        if (_fundInfoGrid != null)
        {
            var children = _fundInfoGrid.Children.OfType<Border>().ToArray();
            if (isCompact)
            {
                if (_fundInfoGrid.ColumnDefinitions.Count >= 3)
                {
                    _fundInfoGrid.ColumnDefinitions[1].Width = new GridLength(0);
                    _fundInfoGrid.ColumnDefinitions[2].Width = new GridLength(0);
                }

                if (children.Length >= 2)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 0); Grid.SetRow(children[1], 2);
                }
            }
            else
            {
                if (_fundInfoGrid.ColumnDefinitions.Count >= 3)
                {
                    _fundInfoGrid.ColumnDefinitions[0].Width = GridLength.Star;
                    _fundInfoGrid.ColumnDefinitions[1].Width = new GridLength(16, GridUnitType.Pixel);
                    _fundInfoGrid.ColumnDefinitions[2].Width = GridLength.Star;
                }

                if (children.Length >= 2)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 2); Grid.SetRow(children[1], 0);
                }
            }
        }

        // ── Subscription info grid: 3 cols → stacked ──
        // XAML pre-declares ColumnDefinitions="*,16,*,16,*" RowDefinitions="Auto,12,Auto,12,Auto"
        if (_subInfoGrid != null)
        {
            var children = _subInfoGrid.Children.OfType<Border>().ToArray();
            if (isCompact)
            {
                if (_subInfoGrid.ColumnDefinitions.Count >= 5)
                {
                    _subInfoGrid.ColumnDefinitions[1].Width = new GridLength(0);
                    _subInfoGrid.ColumnDefinitions[2].Width = new GridLength(0);
                    _subInfoGrid.ColumnDefinitions[3].Width = new GridLength(0);
                    _subInfoGrid.ColumnDefinitions[4].Width = new GridLength(0);
                }

                if (children.Length >= 3)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 0); Grid.SetRow(children[1], 2);
                    Grid.SetColumn(children[2], 0); Grid.SetRow(children[2], 4);
                }
            }
            else
            {
                if (_subInfoGrid.ColumnDefinitions.Count >= 5)
                {
                    _subInfoGrid.ColumnDefinitions[0].Width = GridLength.Star;
                    _subInfoGrid.ColumnDefinitions[1].Width = new GridLength(16, GridUnitType.Pixel);
                    _subInfoGrid.ColumnDefinitions[2].Width = GridLength.Star;
                    _subInfoGrid.ColumnDefinitions[3].Width = new GridLength(16, GridUnitType.Pixel);
                    _subInfoGrid.ColumnDefinitions[4].Width = GridLength.Star;
                }

                if (children.Length >= 3)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 2); Grid.SetRow(children[1], 0);
                    Grid.SetColumn(children[2], 4); Grid.SetRow(children[2], 0);
                }
            }
        }

        // ── Nav bar: hide entire sticky nav + spacer on compact (ShellView's InvestorBackBar provides mobile buttons) ──
        if (_stickyNavBar != null)
            _stickyNavBar.IsVisible = !isCompact;
        if (_navSpacer != null)
            _navSpacer.IsVisible = !isCompact;

        // ── Content margins: compact uses 16px gutter to match floating-bar + sub-tab margins ──
        if (_contentStack != null)
            _contentStack.Margin = isCompact
                ? new Thickness(16, 16, 16, 96)
                : new Thickness(24, 0, 24, 24);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _scroller?.ScrollToHome();
        _navCtaVisible = false;
        if (_navCta != null)
        {
            _navCta.Opacity = 0;
            _navCta.IsHitTestVisible = false;
        }
        UpdateProgressBar();
    }

    private void OnExplorerLinkPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ProjectItemViewModel project && !string.IsNullOrEmpty(project.ProjectId))
        {
            var networkService = App.Services.GetRequiredService<INetworkService>();
            var derivation = App.Services.GetRequiredService<IDerivationOperations>();
            var bitcoinAddress = derivation.ConvertAngorKeyToBitcoinAddress(project.ProjectId);
            ExplorerHelper.OpenAddress(networkService, bitcoinAddress);
        }
        e.Handled = true;
    }

    private void OnViewProjectJsonPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ProjectItemViewModel project && !string.IsNullOrEmpty(project.ProjectId))
        {
            var shell = this.FindAncestorOfType<ShellView>();
            if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
            {
                var projectAppService = App.Services.GetRequiredService<IProjectAppService>();
                var modal = new ProjectInfoJsonModal(project.ProjectId, projectAppService);
                shellVm.ShowModal(modal);
            }
        }
        e.Handled = true;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdateProgressBar();
    }

    private void UpdateProgressBar()
    {
        if (DataContext is ProjectItemViewModel vm)
        {
            if (_progressFill?.Parent is Panel parent)
            {
                // Remove previous handler to prevent accumulation
                if (_parentPropertyChangedHandler != null && _progressParent != null)
                {
                    _progressParent.PropertyChanged -= _parentPropertyChangedHandler;
                }

                _progressParent = parent;
                _parentPropertyChangedHandler = (_, args) =>
                {
                    if (args.Property == BoundsProperty)
                        _progressFill.Width = parent.Bounds.Width * (vm.Progress / 100.0);
                };
                parent.PropertyChanged += _parentPropertyChangedHandler;

                // Initial
                if (parent.Bounds.Width > 0)
                    _progressFill.Width = parent.Bounds.Width * (vm.Progress / 100.0);
            }
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_investBtn == null || _navCta == null || _scroller == null) return;

        // Check if the InvestButton is scrolled out of the visible area.
        var point = _investBtn.TranslatePoint(new Point(0, _investBtn.Bounds.Height), _scroller);

        // If the bottom of the InvestButton is above the top of the viewport, show the nav CTA
        bool shouldShow = point.HasValue && point.Value.Y < 0;

        if (shouldShow != _navCtaVisible)
        {
            _navCtaVisible = shouldShow;
            _navCta.Opacity = shouldShow ? 1 : 0;
            _navCta.IsHitTestVisible = shouldShow;
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        var findProjectsView = this.FindLogicalAncestorOfType<FindProjectsView>();
        if (findProjectsView?.DataContext is FindProjectsViewModel vm)
        {
            vm.CloseProjectDetail();
        }
    }

    private void OnInvestPressed(object? sender, PointerPressedEventArgs e)
    {
        var findProjectsView = this.FindLogicalAncestorOfType<FindProjectsView>();
        if (findProjectsView?.DataContext is FindProjectsViewModel vm)
        {
            vm.OpenInvestPage();
        }
    }

    private void OnSharePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ProjectItemViewModel project)
        {
            var shell = this.FindAncestorOfType<ShellView>();
            if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
            {
                var modal = new ShareModal(project.ProjectName, project.ShortDescription);
                shellVm.ShowModal(modal);
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Unified handler for both collapsible sections (Details and Nostr).
    /// Determines which section was clicked by checking the sender against cached references.
    /// </summary>
    private void OnCollapsibleContainerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender == _detailsContainer)
            ToggleCollapsible(ref _detailsExpanded, _detailsContainer, _detailsHeader, _detailsContent, _detailsChevron, e);
        else if (sender == _nostrContainer)
            ToggleCollapsible(ref _nostrExpanded, _nostrContainer, _nostrHeader, _nostrContent, _nostrChevron, e);
    }

    /// <summary>
    /// Toggle a collapsible section — factored out from the two near-identical handlers.
    /// When collapsed, any click on the container expands it.
    /// When expanded, only clicks within the header area collapse it.
    /// </summary>
    private static void ToggleCollapsible(
        ref bool expanded, Border? container, Border? header,
        StackPanel? content, Control? chevron, PointerPressedEventArgs e)
    {
        if (container == null || header == null) return;

        if (!expanded)
        {
            // Collapsed → expand (click anywhere on container)
            expanded = true;
            if (content != null) content.IsVisible = true;
            chevron?.Classes.Set("ChevronExpanded", true);
            container.Cursor = null;
        }
        else
        {
            // Expanded → collapse only if click is within the header area
            var pos = e.GetPosition(header);
            if (pos.X >= 0 && pos.Y >= 0 && pos.X <= header.Bounds.Width && pos.Y <= header.Bounds.Height)
            {
                expanded = false;
                if (content != null) content.IsVisible = false;
                chevron?.Classes.Set("ChevronExpanded", false);
                container.Cursor = new Cursor(StandardCursorType.Hand);
            }
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }
}

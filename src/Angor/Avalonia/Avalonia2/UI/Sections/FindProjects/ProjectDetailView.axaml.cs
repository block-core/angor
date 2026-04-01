using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Controls;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.FindProjects;

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
        if (_topSectionGrid != null && _topLeftCard != null && _topRightCard != null)
        {
            if (isCompact)
            {
                _topSectionGrid.ColumnDefinitions.Clear();
                _topSectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _topSectionGrid.RowDefinitions.Clear();
                _topSectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _topSectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0, 0, 0, 24);

                Grid.SetColumn(_topRightCard, 0);
                Grid.SetRow(_topRightCard, 1);
            }
            else
            {
                _topSectionGrid.ColumnDefinitions.Clear();
                _topSectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _topSectionGrid.ColumnDefinitions.Add(new ColumnDefinition(400, GridUnitType.Pixel));
                _topSectionGrid.RowDefinitions.Clear();

                Grid.SetColumn(_topLeftCard, 0);
                Grid.SetRow(_topLeftCard, 0);
                _topLeftCard.Margin = new Thickness(0, 0, 24, 0);

                Grid.SetColumn(_topRightCard, 1);
                Grid.SetRow(_topRightCard, 0);
            }
        }

        // ── Stats grid: 3 cols with spacers → stacked ──
        if (_statsGrid != null && _statCard0 != null && _statCard1 != null && _statCard2 != null)
        {
            if (isCompact)
            {
                _statsGrid.ColumnDefinitions.Clear();
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _statsGrid.RowDefinitions.Clear();
                _statsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _statsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _statsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Grid.SetColumn(_statCard0, 0); Grid.SetRow(_statCard0, 0);
                Grid.SetColumn(_statCard1, 0); Grid.SetRow(_statCard1, 1);
                Grid.SetColumn(_statCard2, 0); Grid.SetRow(_statCard2, 2);
                _statCard0.Margin = new Thickness(0, 0, 0, 12);
                _statCard1.Margin = new Thickness(0, 0, 0, 12);
                _statCard2.Margin = new Thickness(0);
            }
            else
            {
                _statsGrid.ColumnDefinitions.Clear();
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(16, GridUnitType.Pixel));
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(16, GridUnitType.Pixel));
                _statsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _statsGrid.RowDefinitions.Clear();

                Grid.SetColumn(_statCard0, 0); Grid.SetRow(_statCard0, 0);
                Grid.SetColumn(_statCard1, 2); Grid.SetRow(_statCard1, 0);
                Grid.SetColumn(_statCard2, 4); Grid.SetRow(_statCard2, 0);
                _statCard0.Margin = new Thickness(0);
                _statCard1.Margin = new Thickness(0);
                _statCard2.Margin = new Thickness(0);
            }
        }

        // ── Investment info grid: 2x2 → stacked ──
        if (_investInfoGrid != null)
        {
            if (isCompact)
            {
                _investInfoGrid.ColumnDefinitions.Clear();
                _investInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _investInfoGrid.RowDefinitions.Clear();
                // 4 cards stacked with 12px gap rows
                for (int i = 0; i < 7; i++)
                    _investInfoGrid.RowDefinitions.Add(new RowDefinition(i % 2 == 0 ? GridLength.Auto : new GridLength(12, GridUnitType.Pixel)));

                // Re-assign children positions
                var children = _investInfoGrid.Children.OfType<Border>().ToArray();
                for (int i = 0; i < children.Length; i++)
                {
                    Grid.SetColumn(children[i], 0);
                    Grid.SetRow(children[i], i * 2);
                }
            }
            else
            {
                _investInfoGrid.ColumnDefinitions.Clear();
                _investInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _investInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(16, GridUnitType.Pixel));
                _investInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _investInfoGrid.RowDefinitions.Clear();
                _investInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _investInfoGrid.RowDefinitions.Add(new RowDefinition(16, GridUnitType.Pixel));
                _investInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                // Restore 2x2 layout
                var children = _investInfoGrid.Children.OfType<Border>().ToArray();
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
        if (_fundInfoGrid != null)
        {
            if (isCompact)
            {
                _fundInfoGrid.ColumnDefinitions.Clear();
                _fundInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _fundInfoGrid.RowDefinitions.Clear();
                _fundInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _fundInfoGrid.RowDefinitions.Add(new RowDefinition(12, GridUnitType.Pixel));
                _fundInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var children = _fundInfoGrid.Children.OfType<Border>().ToArray();
                if (children.Length >= 2)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 0); Grid.SetRow(children[1], 2);
                }
            }
            else
            {
                _fundInfoGrid.ColumnDefinitions.Clear();
                _fundInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _fundInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(16, GridUnitType.Pixel));
                _fundInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _fundInfoGrid.RowDefinitions.Clear();

                var children = _fundInfoGrid.Children.OfType<Border>().ToArray();
                if (children.Length >= 2)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 2); Grid.SetRow(children[1], 0);
                }
            }
        }

        // ── Subscription info grid: 3 cols → stacked ──
        if (_subInfoGrid != null)
        {
            if (isCompact)
            {
                _subInfoGrid.ColumnDefinitions.Clear();
                _subInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _subInfoGrid.RowDefinitions.Clear();
                _subInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _subInfoGrid.RowDefinitions.Add(new RowDefinition(12, GridUnitType.Pixel));
                _subInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _subInfoGrid.RowDefinitions.Add(new RowDefinition(12, GridUnitType.Pixel));
                _subInfoGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var children = _subInfoGrid.Children.OfType<Border>().ToArray();
                if (children.Length >= 3)
                {
                    Grid.SetColumn(children[0], 0); Grid.SetRow(children[0], 0);
                    Grid.SetColumn(children[1], 0); Grid.SetRow(children[1], 2);
                    Grid.SetColumn(children[2], 0); Grid.SetRow(children[2], 4);
                }
            }
            else
            {
                _subInfoGrid.ColumnDefinitions.Clear();
                _subInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _subInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(16, GridUnitType.Pixel));
                _subInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _subInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(16, GridUnitType.Pixel));
                _subInfoGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _subInfoGrid.RowDefinitions.Clear();

                var children = _subInfoGrid.Children.OfType<Border>().ToArray();
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

        // ── Content margins: compact gets equal 24px on all sides + 96px bottom for floating bar ──
        if (_contentStack != null)
            _contentStack.Margin = isCompact
                ? new Thickness(24, 24, 24, 96)
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Angor.Shared;
using Angor.Shared.Services;
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

        // Explorer link — open project txid in block explorer
        var explorerLink = this.FindControl<Border>("ExplorerLink");
        if (explorerLink != null)
            explorerLink.PointerPressed += OnExplorerLinkPressed;

        // Set progress bar width after loaded
        Loaded += OnLoaded;
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
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;
using Avalonia2.UI.Shared.Controls;
using Avalonia.VisualTree;
using ReactiveUI;

namespace Avalonia2.UI.Sections.Home;

public partial class HomeView : UserControl
{
    private IDisposable? _layoutSubscription;

    // Cached controls for responsive layout
    private Grid? _homeGrid;
    private Grid? _fundCard;
    private Grid? _getFundedCard;
    private ScrollableView? _scrollableView;
    private Border? _tiledLogoBorder;

    // Cached controls for mobile sizing
    // Vue mobile (App.vue line 214-241): icons w-10 h-10, text-base titles, text-xs descriptions
    // Vue desktop (App.vue line 2827-2863): icons w-24 h-24, text-3xl titles, 20px descriptions
    private Viewbox? _fundCardIcon;
    private TextBlock? _fundCardTitle;
    private TextBlock? _fundCardDesc;
    private StackPanel? _fundCardContent;
    private Border? _fundCardBtnBorder;
    private Viewbox? _getFundedCardIcon;
    private TextBlock? _getFundedCardTitle;
    private TextBlock? _getFundedCardDesc;
    private StackPanel? _getFundedCardContent;
    private Border? _getFundedBtnBorder;

    /// <summary>Design-time only.</summary>
    public HomeView() => InitializeComponent();

    public HomeView(HomeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Cache responsive layout controls
        _homeGrid = this.FindControl<Grid>("HomeGrid");
        _fundCard = this.FindControl<Grid>("FundCard");
        _getFundedCard = this.FindControl<Grid>("GetFundedCard");
        _scrollableView = this.GetLogicalDescendants().OfType<ScrollableView>().FirstOrDefault();
        _tiledLogoBorder = this.FindControl<Border>("TiledLogoBorder");

        // Cache mobile sizing controls
        _fundCardIcon = this.FindControl<Viewbox>("FundCardIcon");
        _fundCardTitle = this.FindControl<TextBlock>("FundCardTitle");
        _fundCardDesc = this.FindControl<TextBlock>("FundCardDesc");
        _fundCardContent = this.FindControl<StackPanel>("FundCardContent");
        _fundCardBtnBorder = this.FindControl<Border>("FundCardBtnBorder");
        _getFundedCardIcon = this.FindControl<Viewbox>("GetFundedCardIcon");
        _getFundedCardTitle = this.FindControl<TextBlock>("GetFundedCardTitle");
        _getFundedCardDesc = this.FindControl<TextBlock>("GetFundedCardDesc");
        _getFundedCardContent = this.FindControl<StackPanel>("GetFundedCardContent");
        _getFundedBtnBorder = this.FindControl<Border>("GetFundedBtnBorder");

        // ── Responsive layout: two-col (desktop) → stacked (compact) ──
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_homeGrid == null || _fundCard == null || _getFundedCard == null) return;

        // Bottom padding: 96px in compact for tab bar + floating panel clearance
        if (_scrollableView != null)
            _scrollableView.ContentPadding = isCompact
                ? new Thickness(16, 16, 16, 96)  // Vue mobile: p-4 pb-20 (16px padding, 80px bottom for tab bar)
                : new Thickness(24);

        // Tiled logo pattern — show on all platforms
        // Vue: invest-column::before uses background-image: url('/logo.svg') tiled at 80px
        // Previously hidden on mobile for perf, but user noticed it's missing.
        // The VisualBrush with a simple SVG is acceptable on modern mobile GPUs.
        // (The heavier DrawingBrush crosshatch texture in ShellView stays hidden on mobile.)
        if (_tiledLogoBorder != null)
            _tiledLogoBorder.IsVisible = true;

        if (isCompact)
        {
            // ── MOBILE LAYOUT ──
            // Vue mobile (App.vue line 214): flex flex-col h-full gap-3 p-4 pb-20
            // Cards are flex-1 (split available space), stacked with 12px gap
            _homeGrid.ColumnDefinitions.Clear();
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _homeGrid.RowDefinitions.Clear();
            _homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));  // flex-1
            _homeGrid.RowDefinitions.Add(new RowDefinition(12, GridUnitType.Pixel));  // gap-3 = 12px
            _homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));  // flex-1

            Grid.SetColumn(_fundCard, 0);
            Grid.SetRow(_fundCard, 0);
            Grid.SetColumn(_getFundedCard, 0);
            Grid.SetRow(_getFundedCard, 2);

            // Remove MinHeight — let cards flex to fill available space
            _fundCard.MinHeight = 0;
            _getFundedCard.MinHeight = 0;

            // ── Mobile content sizing ──
            // Vue mobile: icons w-10 h-10 (40px) mb-3 (12px)
            ApplyMobileCardSizing(_fundCardIcon, _fundCardTitle, _fundCardDesc, _fundCardContent, _fundCardBtnBorder);
            ApplyMobileCardSizing(_getFundedCardIcon, _getFundedCardTitle, _getFundedCardDesc, _getFundedCardContent, _getFundedBtnBorder);
        }
        else
        {
            // ── DESKTOP LAYOUT ──
            // Vue desktop (App.vue line 2827): min-h-full flex items-stretch
            // Two columns: invest-column p-12 + launch-column p-12, with margin gap
            _homeGrid.ColumnDefinitions.Clear();
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(24, GridUnitType.Pixel));
            _homeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _homeGrid.RowDefinitions.Clear();
            _homeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

            Grid.SetColumn(_fundCard, 0);
            Grid.SetRow(_fundCard, 0);
            Grid.SetColumn(_getFundedCard, 2);
            Grid.SetRow(_getFundedCard, 0);

            // Restore desktop MinHeight
            _fundCard.MinHeight = 480;
            _getFundedCard.MinHeight = 480;

            // ── Desktop content sizing ──
            // Vue desktop: icons w-24 h-24 (96px→80px Avalonia) mb-8 (32px), text-3xl, 20px desc
            ApplyDesktopCardSizing(_fundCardIcon, _fundCardTitle, _fundCardDesc, _fundCardContent, _fundCardBtnBorder);
            ApplyDesktopCardSizing(_getFundedCardIcon, _getFundedCardTitle, _getFundedCardDesc, _getFundedCardContent, _getFundedBtnBorder);
        }
    }

    /// <summary>
    /// Apply mobile sizing to a home card.
    /// Vue mobile: icons w-10 h-10 (40px), mb-3 (12px), text-base title (16px), text-xs desc (12px),
    /// p-4 (16px) padding, full-width button.
    /// </summary>
    private static void ApplyMobileCardSizing(
        Viewbox? icon, TextBlock? title, TextBlock? desc,
        StackPanel? content, Border? btnBorder)
    {
        if (icon != null)
        {
            icon.Width = 40;
            icon.Height = 40;
            icon.Margin = new Thickness(0, 0, 0, 12); // mb-3
        }
        if (title != null)
        {
            title.FontSize = 16; // text-base
            title.Margin = new Thickness(0, 0, 0, 4); // mb-1
            // Remove Title class styling — override with explicit size
            title.Classes.Set("Title", false);
        }
        if (desc != null)
        {
            desc.FontSize = 12; // text-xs
            desc.LineHeight = 18; // leading-relaxed for 12px
            desc.Margin = new Thickness(0, 0, 0, 16); // mb-4
        }
        if (content != null)
        {
            content.Margin = new Thickness(16); // p-4 = 16px
        }
        if (btnBorder != null)
        {
            // Vue mobile: w-full max-w-xs — stretch button, max 320px
            btnBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            btnBorder.MaxWidth = 320;
        }
    }

    /// <summary>
    /// Apply desktop sizing to a home card.
    /// Vue desktop: icons w-24 h-24 (80px), mb-8 (32px), text-3xl title, 20px desc,
    /// p-12 (48px) padding, centered button.
    /// </summary>
    private static void ApplyDesktopCardSizing(
        Viewbox? icon, TextBlock? title, TextBlock? desc,
        StackPanel? content, Border? btnBorder)
    {
        if (icon != null)
        {
            icon.Width = 80;
            icon.Height = 80;
            icon.Margin = new Thickness(0, 0, 0, 32); // mb-8
        }
        if (title != null)
        {
            title.FontSize = double.NaN; // Reset to style default
            title.Margin = new Thickness(0, 0, 0, 16); // mb-4
            title.Classes.Set("Title", true);
        }
        if (desc != null)
        {
            desc.FontSize = 20;
            desc.LineHeight = 32; // leading-relaxed for 20px
            desc.Margin = new Thickness(0, 0, 0, 32); // mb-8
        }
        if (content != null)
        {
            content.Margin = new Thickness(48); // p-12 = 48px
        }
        if (btnBorder != null)
        {
            btnBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            btnBorder.MaxWidth = double.PositiveInfinity;
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        // Find the ShellViewModel via visual tree
        var shell = this.FindAncestorOfType<ShellView>();
        var shellVm = shell?.DataContext as ShellViewModel;
        if (shellVm == null) return;

        // Match by button content text
        var text = GetButtonText(btn);
        switch (text)
        {
            case "Launch a Project":
                shellVm.NavigateToMyProjectsAndLaunch();
                break;
            case "Find Projects":
                shellVm.NavigateToFindProjects();
                break;
        }
    }

    private static string? GetButtonText(Button btn)
    {
        if (btn.Content is string s) return s;
        return null;
    }
}

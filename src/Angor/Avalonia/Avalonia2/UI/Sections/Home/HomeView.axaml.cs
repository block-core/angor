using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;
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

    // Vue mobile uses shorter description text than desktop
    private const string FundDescMobile = "Discover and fund innovative Bitcoin projects.";
    private const string FundDescDesktop = "Discover and fund innovative Bitcoin projects on the Angor platform.";
    private const string GetFundedDescMobile = "Create and launch your own projects.";
    private const string GetFundedDescDesktop = "Create and launch your own projects to raise funding.";

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
        // No ScrollableView wrapping — the HomeGrid fills available space directly,
        // so star rows work correctly on both desktop and mobile.
        _layoutSubscription = LayoutModeService.Instance.WhenAnyValue(x => x.IsCompact)
            .Subscribe(isCompact => ApplyResponsiveLayout(isCompact));
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_homeGrid == null || _fundCard == null || _getFundedCard == null) return;

        // CRITICAL: Never replace ColumnDefinitions/RowDefinitions collections.
        // Only modify existing column/row widths. Replacing collections causes
        // Avalonia's layout engine to crash (SIGABRT) because child controls
        // briefly reference invalid column/row indices during the swap.
        //
        // The XAML Grid always has 3 columns and 3 rows:
        //   Desktop:  Col0=* (card), Col1=24 (gap), Col2=* (card) | Row0=* (content), Row1=0, Row2=0
        //   Compact:  Col0=* (card), Col1=0,        Col2=0        | Row0=* (card),    Row1=16 (gap), Row2=* (card)
        var cols = _homeGrid.ColumnDefinitions;
        var rows = _homeGrid.RowDefinitions;

        if (_tiledLogoBorder != null)
            _tiledLogoBorder.IsVisible = true;

        if (isCompact)
        {
            // ── MOBILE LAYOUT ──
            _homeGrid.Margin = new Thickness(16, 16, 16, 16);

            // Collapse columns 1-2, expand rows for stacked cards
            if (cols.Count >= 3) { cols[0].Width = GridLength.Star; cols[1].Width = new GridLength(0); cols[2].Width = new GridLength(0); }
            if (rows.Count >= 3) { rows[0].Height = GridLength.Star; rows[1].Height = new GridLength(16); rows[2].Height = GridLength.Star; }

            Grid.SetColumn(_fundCard, 0);
            Grid.SetRow(_fundCard, 0);
            Grid.SetColumn(_getFundedCard, 0);
            Grid.SetRow(_getFundedCard, 2);

            _fundCard.MinHeight = 0;
            _getFundedCard.MinHeight = 0;

            ApplyMobileCardSizing(_fundCardIcon, _fundCardTitle, _fundCardDesc, _fundCardContent, _fundCardBtnBorder);
            ApplyMobileCardSizing(_getFundedCardIcon, _getFundedCardTitle, _getFundedCardDesc, _getFundedCardContent, _getFundedBtnBorder);
            if (_fundCardDesc != null) _fundCardDesc.Text = FundDescMobile;
            if (_getFundedCardDesc != null) _getFundedCardDesc.Text = GetFundedDescMobile;
        }
        else
        {
            // ── DESKTOP LAYOUT ──
            _homeGrid.Margin = new Thickness(24);

            // Expand columns for side-by-side, collapse rows 1-2
            if (cols.Count >= 3) { cols[0].Width = GridLength.Star; cols[1].Width = new GridLength(24); cols[2].Width = GridLength.Star; }
            if (rows.Count >= 3) { rows[0].Height = GridLength.Star; rows[1].Height = new GridLength(0); rows[2].Height = new GridLength(0); }

            Grid.SetColumn(_fundCard, 0);
            Grid.SetRow(_fundCard, 0);
            Grid.SetColumn(_getFundedCard, 2);
            Grid.SetRow(_getFundedCard, 0);

            _fundCard.MinHeight = 480;
            _getFundedCard.MinHeight = 480;

            ApplyDesktopCardSizing(_fundCardIcon, _fundCardTitle, _fundCardDesc, _fundCardContent, _fundCardBtnBorder);
            ApplyDesktopCardSizing(_getFundedCardIcon, _getFundedCardTitle, _getFundedCardDesc, _getFundedCardContent, _getFundedBtnBorder);
            if (_fundCardDesc != null) _fundCardDesc.Text = FundDescDesktop;
            if (_getFundedCardDesc != null) _getFundedCardDesc.Text = GetFundedDescDesktop;
        }
    }

    /// <summary>
    /// Apply mobile sizing to a home card.
    /// Sized up from Vue's text-base/text-xs for readability on mobile devices.
    /// </summary>
    private static void ApplyMobileCardSizing(
        Viewbox? icon, TextBlock? title, TextBlock? desc,
        StackPanel? content, Border? btnBorder)
    {
        if (icon != null)
        {
            icon.Width = 48;
            icon.Height = 48;
            icon.Margin = new Thickness(0, 0, 0, 12); // mb-3
        }
        if (title != null)
        {
            title.FontSize = 20; // readable on mobile (Vue text-base=16 is too small on device)
            title.Margin = new Thickness(0, 0, 0, 6);
            title.Classes.Set("Title", false);
        }
        if (desc != null)
        {
            desc.FontSize = 14; // readable on mobile (Vue text-xs=12 is too small on device)
            desc.LineHeight = 20;
            desc.Margin = new Thickness(0, 0, 0, 16);
        }
        if (content != null)
        {
            content.Margin = new Thickness(20); // slightly more breathing room
        }
        if (btnBorder != null)
        {
            // Center the button with a max width cap
            btnBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
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
            title.ClearValue(TextBlock.FontSizeProperty); // Reset to style default
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

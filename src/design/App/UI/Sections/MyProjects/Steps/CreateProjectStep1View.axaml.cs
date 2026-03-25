using Avalonia.Controls;
using Avalonia.Interactivity;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep1View : UserControl
{
    // Type card elements — each card has: border, radio check indicator
    private Border[] _typeCards = [];
    private Viewbox[] _radioChecks = [];
    private string? _selectedType;

    public CreateProjectStep1View()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
        // Apply initial unselected styles so cards don't flash with XAML defaults
        ApplyTypeCardStyles();
    }

    private CreateProjectViewModel? Vm => DataContext as CreateProjectViewModel;

    private void ResolveNamedElements()
    {
        _typeCards =
        [
            this.FindControl<Border>("TypeInvestCard")!,
            this.FindControl<Border>("TypeFundCard")!,
            this.FindControl<Border>("TypeSubscriptionCard")!,
        ];
        _radioChecks =
        [
            this.FindControl<Viewbox>("RadioCheckInvest")!,
            this.FindControl<Viewbox>("RadioCheckFund")!,
            this.FindControl<Viewbox>("RadioCheckSub")!,
        ];

        // Wire up PointerPressed on type cards (Border, not Button)
        var typeNames = new[] { "investment", "fund", "subscription" };
        for (int i = 0; i < _typeCards.Length; i++)
        {
            var typeName = typeNames[i];
            _typeCards[i].PointerPressed += (_, _) =>
            {
                Vm?.SelectProjectType(typeName);
                HighlightTypeCard(typeName);
            };
        }
    }

    #region Type Cards

    /// <summary>
    /// Set the selected type card via CSS class toggling.
    /// Styles handle all visual states — code-behind only toggles TypeCardSelected class
    /// and shows/hides radio check indicators.
    /// </summary>
    public void HighlightTypeCard(string type)
    {
        _selectedType = type;
        ApplyTypeCardStyles();
    }

    /// <summary>
    /// Apply visual states to all three type cards via CSS class toggling.
    /// Zero FindResource, zero ClearValue, zero SolidColorBrush construction.
    /// </summary>
    public void ApplyTypeCardStyles()
    {
        if (_typeCards.Length == 0) return;

        var types = new[] { "investment", "fund", "subscription" };

        for (int i = 0; i < 3; i++)
        {
            var isSelected = types[i] == _selectedType;

            // Toggle selected class on the card border — styles handle everything
            _typeCards[i].Classes.Set("TypeCardSelected", isSelected);

            // Show/hide pre-built radio check indicator
            _radioChecks[i].IsVisible = isSelected;
        }
    }

    /// <summary>
    /// Reset type card visuals to unselected state (called by parent on wizard reset).
    /// </summary>
    public void ResetVisualState()
    {
        _selectedType = null;
        ApplyTypeCardStyles();
    }

    #endregion
}

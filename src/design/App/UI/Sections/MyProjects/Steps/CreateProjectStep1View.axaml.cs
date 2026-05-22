using Avalonia.Controls;
using Avalonia.Interactivity;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep1View : UserControl
{
    // Type card elements — each card has: button, radio check indicator
    private Button[] _typeCards = [];
    private Viewbox[] _radioChecks = [];
    private string? _selectedType;

    public CreateProjectStep1View()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
        ApplyTypeCardStyles();
    }

    private CreateProjectViewModel? Vm => DataContext as CreateProjectViewModel;

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        var typeNames = new Dictionary<string, string>
        {
            ["TypeInvestCard"] = "investment",
            ["TypeFundCard"] = "fund",
            ["TypeSubscriptionCard"] = "subscription",
        };

        if (btn.Name != null && typeNames.TryGetValue(btn.Name, out var typeName))
        {
            Vm?.SelectProjectType(typeName);
            HighlightTypeCard(typeName);
        }
    }

    private void ResolveNamedElements()
    {
        _typeCards =
        [
            this.FindControl<Button>("TypeInvestCard")!,
            this.FindControl<Button>("TypeFundCard")!,
            this.FindControl<Button>("TypeSubscriptionCard")!,
        ];
        _radioChecks =
        [
            this.FindControl<Viewbox>("RadioCheckInvest")!,
            this.FindControl<Viewbox>("RadioCheckFund")!,
            this.FindControl<Viewbox>("RadioCheckSub")!,
        ];
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

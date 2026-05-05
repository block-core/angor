using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace App.UI.Shared.Controls;

/// <summary>
/// A reusable empty-state placeholder shown when a section has no data.
/// Vue: All sections except Funders show the Angor logo centered at 112x112 with
/// opacity 0.9. Funders uniquely shows the Users icon inside a circular background.
/// Each section has its own title, description, and optional CTA button with optional icon.
/// </summary>
public class EmptyState : TemplatedControl
{
    private Button? ctaButton;

    static EmptyState()
    {
        ButtonIconValueProperty.Changed.AddClassHandler<EmptyState>((x, _) =>
            x.HasButtonFaIcon = !string.IsNullOrEmpty(x.ButtonIconValue));
    }

    /// <summary>
    /// When true, shows a custom icon (IconData) inside a circle instead of the Angor logo.
    /// Used only for the Funders section.
    /// </summary>
    public static readonly StyledProperty<bool> UseCustomIconProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(UseCustomIcon));

    /// <summary>
    /// The custom icon geometry (only used when UseCustomIcon is true).
    /// </summary>
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(IconData));

    /// <summary>
    /// Whether the custom icon should use Fill (true) or Stroke (false) rendering.
    /// </summary>
    public static readonly StyledProperty<bool> IconIsFilledProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IconIsFilled));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ButtonTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ButtonText));

    public static readonly StyledProperty<bool> HasButtonProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(HasButton));

    /// <summary>
    /// Optional icon to show inside the CTA button (e.g. Plus for "Add Wallet", Search for "Find Projects").
    /// When null, the button shows text only.
    /// </summary>
    public static readonly StyledProperty<Geometry?> ButtonIconDataProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(ButtonIconData));

    /// <summary>
    /// Whether the button icon uses Fill rendering (true) or Stroke rendering (false, default).
    /// </summary>
    public static readonly StyledProperty<bool> ButtonIconIsFilledProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(ButtonIconIsFilled));

    /// <summary>
    /// Whether the button has an icon. Set to True when ButtonIconData is provided.
    /// </summary>
    public static readonly StyledProperty<bool> HasButtonIconProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(HasButtonIcon));

    /// <summary>
    /// FontAwesome icon value for the CTA button (e.g. "fa-solid fa-plus").
    /// When set, an i:Icon is rendered instead of the Path-based ButtonIconData.
    /// Setting this automatically sets HasButtonFaIcon to true.
    /// </summary>
    public static readonly StyledProperty<string?> ButtonIconValueProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ButtonIconValue));

    /// <summary>
    /// Auto-set to true when ButtonIconValue is provided. Controls visibility of the FA icon in the button.
    /// </summary>
    public static readonly StyledProperty<bool> HasButtonFaIconProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(HasButtonFaIcon));

    /// <summary>
    /// Font size for the description text. Default 14 (Vue text-sm).
    /// Set to 20 for My Projects and Funded sections (Vue: font-size: 20px !important).
    /// </summary>
    public static readonly StyledProperty<double> DescriptionFontSizeProperty =
        AvaloniaProperty.Register<EmptyState, double>(nameof(DescriptionFontSize), 14);

    public bool UseCustomIcon
    {
        get => GetValue(UseCustomIconProperty);
        set => SetValue(UseCustomIconProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public bool IconIsFilled
    {
        get => GetValue(IconIsFilledProperty);
        set => SetValue(IconIsFilledProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? ButtonText
    {
        get => GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public bool HasButton
    {
        get => GetValue(HasButtonProperty);
        set => SetValue(HasButtonProperty, value);
    }

    public Geometry? ButtonIconData
    {
        get => GetValue(ButtonIconDataProperty);
        set => SetValue(ButtonIconDataProperty, value);
    }

    public bool ButtonIconIsFilled
    {
        get => GetValue(ButtonIconIsFilledProperty);
        set => SetValue(ButtonIconIsFilledProperty, value);
    }

    public bool HasButtonIcon
    {
        get => GetValue(HasButtonIconProperty);
        set => SetValue(HasButtonIconProperty, value);
    }

    public string? ButtonIconValue
    {
        get => GetValue(ButtonIconValueProperty);
        set => SetValue(ButtonIconValueProperty, value);
    }

    public bool HasButtonFaIcon
    {
        get => GetValue(HasButtonFaIconProperty);
        set => SetValue(HasButtonFaIconProperty, value);
    }

    public double DescriptionFontSize
    {
        get => GetValue(DescriptionFontSizeProperty);
        set => SetValue(DescriptionFontSizeProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? ButtonClick;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (ctaButton != null)
            ctaButton.Click -= OnButtonClick;

        ctaButton = e.NameScope.Find<Button>("CtaButton");
        if (ctaButton != null)
            ctaButton.Click += OnButtonClick;
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e) => ButtonClick?.Invoke(this, e);
}

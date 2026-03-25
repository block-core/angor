using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using App.UI.Shared.Helpers;

namespace App.UI.Shared.Controls;

/// <summary>
/// A project card for the Find Projects grid. Vue shows:
/// banner image, avatar, pills (type + status), title, description,
/// stats row (investor count + raised BTC), target row (label + amount),
/// progress bar at the very bottom.
/// </summary>
public class ProjectCard : TemplatedControl
{
    public static readonly StyledProperty<string?> BannerProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(Banner));

    public static readonly StyledProperty<string?> AvatarProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(Avatar));

    /// <summary>Decoded bitmap for the banner image, loaded from <see cref="Banner"/> URL via ImageCacheService.</summary>
    public static readonly StyledProperty<Bitmap?> BannerBitmapProperty =
        AvaloniaProperty.Register<ProjectCard, Bitmap?>(nameof(BannerBitmap));

    /// <summary>Decoded bitmap for the avatar image, loaded from <see cref="Avatar"/> URL via ImageCacheService.</summary>
    public static readonly StyledProperty<Bitmap?> AvatarBitmapProperty =
        AvaloniaProperty.Register<ProjectCard, Bitmap?>(nameof(AvatarBitmap));

    static ProjectCard()
    {
        BannerProperty.Changed.AddClassHandler<ProjectCard>((card, _) =>
            ImageCacheService.LoadBitmapAsync(card.Banner, bmp => card.BannerBitmap = bmp));
        AvatarProperty.Changed.AddClassHandler<ProjectCard>((card, _) =>
            ImageCacheService.LoadBitmapAsync(card.Avatar, bmp => card.AvatarBitmap = bmp));
    }

    public static readonly StyledProperty<string?> ProjectNameProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(ProjectName));

    public static readonly StyledProperty<string?> ShortDescriptionProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(ShortDescription));

    public static readonly StyledProperty<int> InvestorCountProperty =
        AvaloniaProperty.Register<ProjectCard, int>(nameof(InvestorCount));

    /// <summary>
    /// Label for the investor count: "Investors", "Funders", or "Subscribers"
    /// depending on ProjectType. Set from ViewModel.
    /// </summary>
    public static readonly StyledProperty<string?> InvestorLabelProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(InvestorLabel), "Investors");

    public static readonly StyledProperty<string?> RaisedProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(Raised));

    public static readonly StyledProperty<string?> TargetProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(Target));

    /// <summary>
    /// Label for the target row: "Target:" (invest), "Goal:" (fund), or "Total Subscribers:" (subscription).
    /// </summary>
    public static readonly StyledProperty<string?> TargetLabelProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(TargetLabel), "Target:");

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProjectCard, double>(nameof(Progress));

    public static readonly StyledProperty<string?> ProjectTypeProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(ProjectType));

    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(Status), "Open");

    /// <summary>
    /// When true, shows "Manage Project" + Share buttons at the bottom of the card.
    /// Vue: showManageFunds prop on ProjectCard — only set on My Projects page.
    /// </summary>
    public static readonly StyledProperty<bool> ShowManageFundsProperty =
        AvaloniaProperty.Register<ProjectCard, bool>(nameof(ShowManageFunds));

    /// <summary>
    /// Vue Find Projects cards always show amounts. This property is kept for
    /// backwards compatibility but amounts are now shown by default.
    /// </summary>
    public static readonly StyledProperty<bool> ShowAmountsProperty =
        AvaloniaProperty.Register<ProjectCard, bool>(nameof(ShowAmounts), true);

    public string? Banner
    {
        get => GetValue(BannerProperty);
        set => SetValue(BannerProperty, value);
    }

    public string? Avatar
    {
        get => GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    public Bitmap? BannerBitmap
    {
        get => GetValue(BannerBitmapProperty);
        set => SetValue(BannerBitmapProperty, value);
    }

    public Bitmap? AvatarBitmap
    {
        get => GetValue(AvatarBitmapProperty);
        set => SetValue(AvatarBitmapProperty, value);
    }

    public string? ProjectName
    {
        get => GetValue(ProjectNameProperty);
        set => SetValue(ProjectNameProperty, value);
    }

    public string? ShortDescription
    {
        get => GetValue(ShortDescriptionProperty);
        set => SetValue(ShortDescriptionProperty, value);
    }

    public int InvestorCount
    {
        get => GetValue(InvestorCountProperty);
        set => SetValue(InvestorCountProperty, value);
    }

    public string? InvestorLabel
    {
        get => GetValue(InvestorLabelProperty);
        set => SetValue(InvestorLabelProperty, value);
    }

    public string? Raised
    {
        get => GetValue(RaisedProperty);
        set => SetValue(RaisedProperty, value);
    }

    public string? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public string? TargetLabel
    {
        get => GetValue(TargetLabelProperty);
        set => SetValue(TargetLabelProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string? ProjectType
    {
        get => GetValue(ProjectTypeProperty);
        set => SetValue(ProjectTypeProperty, value);
    }

    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public bool ShowManageFunds
    {
        get => GetValue(ShowManageFundsProperty);
        set => SetValue(ShowManageFundsProperty, value);
    }

    public bool ShowAmounts
    {
        get => GetValue(ShowAmountsProperty);
        set => SetValue(ShowAmountsProperty, value);
    }
}

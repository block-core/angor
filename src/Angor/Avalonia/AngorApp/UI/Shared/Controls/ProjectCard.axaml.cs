using Avalonia;
using Avalonia.Controls.Primitives;

namespace AngorApp.UI.Shared.Controls;

public class ProjectCard : TemplatedControl
{
    public static readonly StyledProperty<Uri?> BannerProperty = AvaloniaProperty.Register<ProjectCard, Uri?>(nameof(Banner));
    public static readonly StyledProperty<Uri?> AvatarProperty = AvaloniaProperty.Register<ProjectCard, Uri?>(nameof(Avatar));
    public static readonly StyledProperty<string?> ProjectNameProperty = AvaloniaProperty.Register<ProjectCard, string?>(nameof(ProjectName));
    public static readonly StyledProperty<string?> ShortDescriptionProperty = AvaloniaProperty.Register<ProjectCard, string?>(nameof(ShortDescription));

    public Uri? Banner
    {
        get => GetValue(BannerProperty);
        set => SetValue(BannerProperty, value);
    }

    public Uri? Avatar
    {
        get => GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
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
}
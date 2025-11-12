using Avalonia;
using Avalonia.Controls.Primitives;

namespace AngorApp.UI.Shared.Controls;

public class Header : TemplatedControl
{
    public static readonly StyledProperty<Uri?> IconUriProperty = AvaloniaProperty.Register<Header, Uri?>(
        nameof(IconUri));

    public Uri? IconUri
    {
        get => GetValue(IconUriProperty);
        set => SetValue(IconUriProperty, value);
    }

    public static readonly StyledProperty<Uri?> BackgroundImageUriProperty = AvaloniaProperty.Register<Header, Uri?>(
        nameof(BackgroundImageUri));

    public Uri? BackgroundImageUri
    {
        get => GetValue(BackgroundImageUriProperty);
        set => SetValue(BackgroundImageUriProperty, value);
    }

    public static readonly StyledProperty<Thickness> IconMarginProperty = AvaloniaProperty.Register<Header, Thickness>(
        nameof(IconMargin));

    public Thickness IconMargin
    {
        get => GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }
}
using System.Reflection;
using Avalonia.Interactivity;
using Avalonia.Media;
using Optris.Icons.Avalonia;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Runtime guard for the Optris.Icons.Avalonia "black icon" bug.
///
/// Root cause: <see cref="Icon"/> bakes <c>Foreground ?? Brushes.Black</c> into an
/// internal <see cref="IconImage"/> snapshot whenever Value/Foreground changes. During
/// template instantiation (e.g. icons inside a TemplatedControl's ControlTemplate with
/// a DynamicResource Foreground, like ProjectCard's syncing indicator) the final bake
/// can fire while Foreground still evaluates to null — leaving a permanently BLACK
/// icon even though the Foreground property later reports the correct themed brush.
///
/// Fix: on every Icon's Loaded event (styles/resources fully applied), compare the
/// baked brush against the effective Foreground and re-bake when they diverge.
/// The global default foreground for icons lives in UI/Themes/V2/Styles/Icons.axaml
/// (<c>Style Selector="i|Icon"</c>). Guarded by DarkModeIconTests in App.Test.Integration.
/// </summary>
public static class IconBakeFix
{
    private static readonly PropertyInfo? ImageProperty =
        typeof(Icon).GetProperty("Image", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static bool installed;

    /// <summary>Installs a class handler that repairs stale icon bakes. Idempotent.</summary>
    public static void Install()
    {
        if (installed) return;
        installed = true;

        Avalonia.Controls.Control.LoadedEvent.AddClassHandler(
            typeof(Icon),
            (sender, _) => Rebake((Icon)sender!),
            RoutingStrategies.Direct);
    }

    private static void Rebake(Icon icon)
    {
        IBrush? foreground = icon.Foreground;
        if (foreground is null)
            return;

        if (ImageProperty?.GetValue(icon) is not IconImage image)
            return;

        if (BrushesMatch(image.Brush, foreground))
            return;

        // Re-bake with the effective foreground (mirrors the package's own
        // OnPropertyChanged path, which was skipped by the ordering hole).
        ImageProperty.SetValue(icon, new IconImage(icon.Value ?? string.Empty, foreground));
    }

    private static bool BrushesMatch(IBrush? baked, IBrush foreground)
    {
        if (ReferenceEquals(baked, foreground))
            return true;

        return baked is ISolidColorBrush a
               && foreground is ISolidColorBrush b
               && a.Color == b.Color;
    }
}

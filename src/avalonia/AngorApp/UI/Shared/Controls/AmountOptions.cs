using Avalonia;
using Avalonia.Styling;

namespace AngorApp.UI.Shared.Controls;

public static class AmountOptions
{
    public static readonly AttachedProperty<bool> IsBitcoinPreferredProperty =
        AvaloniaProperty.RegisterAttached<StyledElement, bool>(
            "IsBitcoinPreferred", typeof(AmountOptions), inherits: true);

    public static bool GetIsBitcoinPreferred(AvaloniaObject element) => element.GetValue(IsBitcoinPreferredProperty);
    public static void SetIsBitcoinPreferred(AvaloniaObject element, bool value) => element.SetValue(IsBitcoinPreferredProperty, value);
}

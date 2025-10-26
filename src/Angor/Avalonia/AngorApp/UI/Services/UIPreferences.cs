namespace AngorApp.UI.Services;

public sealed record UIPreferences(bool IsBitcoinPreferred, bool IsDarkThemeEnabled)
{
    public static UIPreferences CreateDefault() => new(true, false);
}

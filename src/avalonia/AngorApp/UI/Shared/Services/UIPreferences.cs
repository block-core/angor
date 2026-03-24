namespace AngorApp.UI.Shared.Services;

public sealed record UIPreferences(bool IsBitcoinPreferred, bool IsDarkThemeEnabled, bool IsDebugModeEnabled)
{
    public static UIPreferences CreateDefault() => new(true, false, false);
}

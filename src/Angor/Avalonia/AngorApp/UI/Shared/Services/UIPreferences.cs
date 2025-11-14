namespace AngorApp.UI.Shared.Services;

public sealed record UIPreferences(
    bool IsBitcoinPreferred,
    bool IsDarkThemeEnabled,
    bool IsDebugModeEnabled,
    string SelectedNetwork = "Angornet")
{
    public static UIPreferences CreateDefault() => new(true, false, false, "Angornet");
}

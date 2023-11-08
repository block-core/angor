namespace Angor.Shared.Models;

public class SettingsInfo
{
    public List<SettingsUrl> Indexers { get; set; } = new();
    public List<SettingsUrl> Relays { get; set; } = new();
}

public class SettingsUrl
{
    public string Name { get; set; }
    public string Url { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsOnline { get; set; }
}


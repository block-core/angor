namespace Angor.Shared.Models;

public class SettingsInfo
{
    public List<SettingsUrl> Indexers { get; set; } = new();
    public List<SettingsUrl> Relays { get; set; } = new();
    public List<SettingsUrl> Explorers { get; set; } = new();
    public List<SettingsUrl> ChatApps { get; set; } = new();
    public List<SettingsUrl> ImageServers { get; set; } = new();
}

public class SettingsUrl
{
    public string Name { get; set; }
    public string Url { get; set; }

    public bool IsPrimary { get; set; }

    public UrlStatus Status { get; set; }

    public DateTime LastCheck { get; set; }
}

public enum UrlStatus
{
    Unknown,
    Offline,
    NotReady,
    Online
}

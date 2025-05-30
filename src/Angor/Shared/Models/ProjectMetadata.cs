using Newtonsoft.Json;
using Nostr.Client.Messages.Metadata;

namespace Angor.Shared.Models;

public class ProjectMetadata
{
    public string Name { get; set; }
    [JsonProperty("display_name")]
    public string DisplayName { get; set; }
    public string Website { get; set; }
    public string About { get; set; }
    public string Picture { get; set; }
    public string Nip05 { get; set; }
    public string Lud16 { get; set; }
    public string Banner { get; set; }
    public string Nip57 { get; set; }

    public static ProjectMetadata Parse(NostrMetadata nostrMetadata)
    {
        var project = new ProjectMetadata
        {
            Nip57 = nostrMetadata.Nip57,
            Lud16 = nostrMetadata.Nip57,
            Nip05 = nostrMetadata.Nip57,
            About = nostrMetadata.About,
            Banner = nostrMetadata.Banner,
            Picture = nostrMetadata.Picture,
            Name = nostrMetadata.Name,
            Website = nostrMetadata.Website,
            DisplayName = nostrMetadata.DisplayName
        };

        return project;
    }

    public NostrMetadata ToNostrMetadata()
    {
        var nostr = new NostrMetadata
        {
            About = About,
            Banner = Banner,
            Lud16 = Lud16,
            Name = Name,
            Nip05 = Nip05,
            Nip57 = Nip57,
            Picture = Picture,
            Website = Website,
            DisplayName = DisplayName
        };

        return nostr;
    }
}
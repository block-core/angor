using Nostr.Client.Messages.Metadata;

namespace Angor.Shared.Models;

public class ProjectMetadata
{
    private const string displayName = "display_name";
    
    public string Name { get; set; }
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
            Name = nostrMetadata.Name
        };
        if (nostrMetadata.AdditionalData.ContainsKey(nameof(project.Website)))
            project.Website = nostrMetadata.AdditionalData[nameof(project.Website)].ToString();
        if (nostrMetadata.AdditionalData.ContainsKey(displayName))
            project.Name = nostrMetadata.AdditionalData[displayName].ToString();
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
            AdditionalData = new (){{displayName,Name}}
        };

        if (Website != null) 
            nostr.AdditionalData.Add("website",Website);
        
        return nostr;
    }
}
using Angor.Shared.Models;

namespace Angor.Client.Models;

public class FounderProject : Project
{
    public int ProjectIndex { get; set; }
    public DateTime? LastRequestForSignaturesTime { get; set; }

    public string ProjectInfoEventId { get; set; }
    public bool NostrProfileCreated { get; set; }
    
    public bool NostrMetadataCreated()
    {
        return !string.IsNullOrEmpty(Metadata?.Name);
    }

    public bool NostrApplicationSpecificDataCreated()
    {
        return !string.IsNullOrEmpty(ProjectInfoEventId);
    }
}
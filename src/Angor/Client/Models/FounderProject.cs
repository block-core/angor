using Angor.Shared.Models;

namespace Angor.Client.Models;

public class FounderProject : Project
{
    public int ProjectIndex { get; set; }
    public DateTime? LastRequestForSignaturesTime { get; set; }

    public bool NostrMetadataCreated { get; set; }
    public bool NostrApplicationSpecificDataCreated { get; set; }
}
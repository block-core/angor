using Angor.Shared.Models;

namespace Angor.Client.Models;

public class FounderProject
{
    public ProjectMetadata? Metadata { get; set; }
    public ProjectInfo ProjectInfo { get; set; }
    public DateTime? LastRequestForSignaturesTime { get; set; }
}
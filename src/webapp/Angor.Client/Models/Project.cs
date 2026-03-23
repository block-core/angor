using Angor.Shared.Models;

namespace Angor.Client.Models;

public class Project
{
    public ProjectMetadata? Metadata { get; set; }
    public ProjectInfo ProjectInfo { get; set; }

    public string? CreationTransactionId { get; set; }

}
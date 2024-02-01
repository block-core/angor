using Angor.Client.Services;
using Angor.Shared.Models;

namespace Angor.Client.Models;

public class Project
{
    public Project()
    { }

    public Project(ProjectIndexerData indexerData)
    {
        ProjectInfo = new ProjectInfo
        {
            ProjectIdentifier = indexerData.ProjectIdentifier,
            FounderKey = indexerData.FounderKey,
            NostrPubKey = indexerData.NostrPubKey,
            CreationTransactionId = indexerData.TrxId
        };
    }
    
    public ProjectMetadata? Metadata { get; set; }
    public ProjectInfo ProjectInfo { get; set; }
    
    
}
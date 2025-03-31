using Angor.Shared.Models;
using Angor.Shared.Services;

namespace Angor.UI.Model.Implementation.Projects;

public record ProjectData
{
    public ProjectIndexerData IndexerData { get; init; }
    public ProjectInfo ProjectInfo { get; init; }
    public ProjectMetadata NostrMetadata { get; init; }
}
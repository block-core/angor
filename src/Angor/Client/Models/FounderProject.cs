using Angor.Shared.Models;

namespace Angor.Client.Models;

public class FounderProject
{
    public ProjectInfo ProjectInfo { get; set; }
    public DateTime? LastRequestSignedTime { get; set; }
}